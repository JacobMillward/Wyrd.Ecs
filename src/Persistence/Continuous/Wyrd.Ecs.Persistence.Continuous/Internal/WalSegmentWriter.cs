using System.Text;

namespace Wyrd.Ecs.Persistence.Continuous.Internal;

/// <summary>
/// Owns the currently-open WAL segment. Lock-guarded because <see cref="Rotate"/> is
/// called from the checkpoint-merge thread while <see cref="WriteRecords"/>/<see cref="Flush"/>
/// run on the WAL-writer thread; the lock makes swapping the stream reference safe
/// across both. Opens a segment lazily on first use and never resumes a closed one.
/// </summary>
internal sealed class WalSegmentWriter(IWalStore walStore)
{
    private readonly object _lock = new();
    // A field initializer can't reference another instance field (CS0236), so the
    // reusable buffer is recovered from the writer's own BaseStream instead of held as
    // a second field.
    private readonly BinaryWriter _recordWriter = new(new MemoryStream(), Encoding.UTF8, leaveOpen: true);
    private MemoryStream RecordBuffer => (MemoryStream)_recordWriter.BaseStream;
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
    /// Writes every entry to the currently open segment, encoding any pending value change
    /// as it goes. <see cref="DrainedChanges.Ready"/> and <see cref="DrainedChanges.Pending"/>
    /// are merged by <c>Tick</c> (ready first on a tie), not written as two separate blocks:
    /// since <see cref="CheckpointBuilder.Apply"/> replays records strictly in write order,
    /// writing all ready before all pending could put a stale <c>ComponentChanged</c> after
    /// a later <c>EntityDestroyed</c> for the same entity, silently resurrecting it on
    /// replay. Throws if no segment is open; call <see cref="EnsureSegmentOpen"/> first.
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
                    if (entry.Kind is WalRecordKind.RelationLinked or WalRecordKind.RelationUnlinked)
                        WalSegmentIO.WriteRelationRecord(_currentSegment, RecordBuffer, _recordWriter, entry.Kind, entry.Tick, entry.EntityId, entry.TargetId!.Value, entry.Discriminator, entry.SchemaHash, entry.Payload);
                    else
                        WalSegmentIO.WriteRecord(_currentSegment, RecordBuffer, _recordWriter, entry.Kind, entry.Tick, entry.EntityId, entry.Discriminator, entry.SchemaHash, entry.Payload);
                }
                else
                {
                    var pending = changes.Pending[pendingIndex++];
                    WalSegmentIO.WriteRecord(_currentSegment, RecordBuffer, _recordWriter, WalRecordKind.ComponentChanged, pending.Tick, pending.EntityId, pending.Codec.Discriminator, pending.Codec.SchemaHash, pending.Codec.EncodeValue(pending.Value));
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

    /// <summary>Closes the currently open segment (if any) without opening a replacement, for a final shutdown merge where no further writing will happen.</summary>
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
