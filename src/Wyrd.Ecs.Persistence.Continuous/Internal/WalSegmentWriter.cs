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

    /// <summary>Writes every entry to the currently open segment. Throws if none is open — call <see cref="EnsureSegmentOpen"/> first.</summary>
    internal void WriteRecords(IReadOnlyList<CapturedWalEntry> entries)
    {
        lock (_lock)
        {
            if (_currentSegment is null)
                throw new InvalidOperationException("No WAL segment is open. Call EnsureSegmentOpen first.");

            foreach (var entry in entries)
                WalSegmentIO.WriteRecord(_currentSegment, entry.Kind, entry.Tick, entry.EntityId, entry.Discriminator, entry.SchemaHash, entry.Payload);
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
