namespace Wyrd.Ecs.Persistence.Continuous.Internal;

/// <summary>
/// Owns the currently-open WAL segment. Lock-guarded, not because ordinary writes and
/// reads race each other (only the WAL-writer thread ever calls
/// <see cref="WriteRecords"/>/<see cref="Flush"/>) but because <see cref="Rotate"/> is
/// called from the checkpoint-merge thread as part of the rotation handoff — the lock
/// makes swapping the underlying stream reference safe regardless of which thread calls
/// what, when. Opens a segment lazily on first use and never resumes a closed one.
/// </summary>
internal sealed class WalSegmentWriter(IWalStore walStore)
{
    private readonly object _lock = new();
    private Stream? _currentSegment;
    private int _currentSegmentStartTick = -1;

    /// <summary>The starting tick of the segment currently open for writing, or -1 if none is open yet.</summary>
    internal int CurrentSegmentStartTick
    {
        get { lock (_lock) return _currentSegmentStartTick; }
    }

    /// <summary>Opens a fresh segment starting at <paramref name="startTick"/> if none is currently open; a no-op otherwise.</summary>
    internal void EnsureSegmentOpen(int startTick)
    {
        lock (_lock)
        {
            if (_currentSegment is not null) return;
            _currentSegment = walStore.OpenSegmentAppend(startTick);
            WalSegmentIO.WriteHeader(_currentSegment);
            _currentSegmentStartTick = startTick;
        }
    }

    /// <summary>
    /// Writes every entry to the currently open segment, encoding any pending value
    /// change as it goes. <see cref="DrainedChanges.Ready"/> and
    /// <see cref="DrainedChanges.Pending"/> are merged by <c>Tick</c> (stable, ready
    /// first on a tie) rather than written as two separate blocks — a single drain
    /// cycle can span several ticks (nothing forces a drain between every tick), and
    /// <see cref="CheckpointBuilder.Apply"/> replays records strictly in write order
    /// with no tick-aware reordering of its own. Writing every ready record before
    /// every pending one would let an earlier tick's stale <c>ComponentChanged</c> land
    /// after a later tick's <c>EntityDestroyed</c> for the same entity in the file,
    /// silently resurrecting it on replay. Throws if none is open — call
    /// <see cref="EnsureSegmentOpen"/> first.
    /// </summary>
    internal void WriteRecords(DrainedChanges changes)
    {
        lock (_lock)
        {
            if (_currentSegment is null)
                throw new InvalidOperationException("No WAL segment is open. Call EnsureSegmentOpen first.");

            var readyIndex = 0;
            var pendingIndex = 0;
            while (readyIndex < changes.Ready.Count || pendingIndex < changes.Pending.Count)
            {
                var writeReady =
                    pendingIndex >= changes.Pending.Count ||
                    (readyIndex < changes.Ready.Count && changes.Ready[readyIndex].Tick <= changes.Pending[pendingIndex].Tick);

                if (writeReady)
                {
                    var entry = changes.Ready[readyIndex++];
                    WalSegmentIO.WriteRecord(_currentSegment, entry.Kind, entry.Tick, entry.EntityId, entry.Discriminator, entry.SchemaHash, entry.Payload);
                }
                else
                {
                    var pending = changes.Pending[pendingIndex++];
                    WalSegmentIO.WriteRecord(_currentSegment, WalRecordKind.ComponentChanged, pending.Tick, pending.EntityId, pending.Codec.Discriminator, pending.Codec.SchemaHash, pending.Codec.EncodeValue(pending.Value));
                }
            }
        }
    }

    /// <summary>Durably flushes the currently open segment. A no-op if none is open.</summary>
    internal void Flush()
    {
        lock (_lock)
        {
            if (_currentSegment is not null) walStore.Flush(_currentSegment);
        }
    }

    /// <summary>Closes the currently open segment (if any) without opening a replacement — for a final shutdown merge where no further writing will happen.</summary>
    internal void CloseCurrentSegment()
    {
        lock (_lock)
        {
            _currentSegment?.Dispose();
            _currentSegment = null;
            _currentSegmentStartTick = -1;
        }
    }

    /// <summary>Closes the currently open segment (if any) and opens a fresh one starting at <paramref name="newStartTick"/>.</summary>
    internal void Rotate(int newStartTick)
    {
        lock (_lock)
        {
            _currentSegment?.Dispose();
            _currentSegment = null;
            EnsureSegmentOpen(newStartTick);
        }
    }
}
