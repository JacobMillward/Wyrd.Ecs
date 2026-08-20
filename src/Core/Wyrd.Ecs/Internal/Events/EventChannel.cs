namespace Wyrd.Ecs.Internal;

/// <summary>
/// A double-buffered event log for one event type, retained across exactly two
/// <see cref="Swap"/> calls: written this tick, still readable next tick, dropped the tick
/// after that. Mirrors Bevy's <c>Events&lt;T&gt;</c> double buffer. One instance per distinct
/// event type, created lazily by <see cref="World"/> on first use.
/// </summary>
internal sealed class EventChannel<T> : IEventChannel where T : struct
{
    private readonly Lock _gate = new();
    private T[] _older = [];
    private T[] _newer = [];
    private int _olderCount;
    private int _newerCount;
    private long _countBeforeOlder;

    /// <summary>Appends <paramref name="value"/>. Safe to call concurrently from several threads at once.</summary>
    internal void Write(T value)
    {
        lock (_gate)
        {
            ArrayGrowth.EnsureCapacity(ref _newer, _newerCount + 1);
            _newer[_newerCount++] = value;
        }
    }

    /// <summary>
    /// This channel's current write count, under lock: the cursor a brand-new
    /// <see cref="EventReader{T}"/> starts from, so it never sees anything written before it
    /// was created.
    /// </summary>
    internal long SnapshotCursor()
    {
        lock (_gate) return _countBeforeOlder + _olderCount + _newerCount;
    }

    /// <summary>
    /// Appends every event written since <paramref name="cursor"/> into
    /// <paramref name="destination"/> (cleared first), and returns the cursor to store for
    /// next time. Anything before this channel's oldest retained event is silently dropped by
    /// clamping <paramref name="cursor"/> up first - a caller that goes more than one tick
    /// between calls can lose events this way; see <see cref="EventReader{T}"/>'s own doc.
    /// </summary>
    internal long Read(long cursor, List<T> destination)
    {
        lock (_gate)
        {
            destination.Clear();
            if (cursor < _countBeforeOlder) cursor = _countBeforeOlder;

            var olderStart = (int)(cursor - _countBeforeOlder);
            for (var i = olderStart; i < _olderCount; i++)
                destination.Add(_older[i]);

            var newerStart = Math.Max(0, (int)(cursor - _countBeforeOlder - _olderCount));
            for (var i = newerStart; i < _newerCount; i++)
                destination.Add(_newer[i]);

            return _countBeforeOlder + _olderCount + _newerCount;
        }
    }

    /// <summary>
    /// Retires <c>_older</c>, promotes <c>_newer</c> in its place, and ping-pongs the
    /// discarded backing array back in as the next tick's write target - no per-tick
    /// allocation in steady state. Takes no lock: only ever called from
    /// <see cref="World.AdvanceTick"/>, before that tick's systems have started, so nothing
    /// else can be touching this channel concurrently - same reasoning
    /// <see cref="CommandBuffer.Apply"/>'s own doc gives for skipping its lock.
    /// </summary>
    public void Swap()
    {
        (_older, _newer) = (_newer, _older);
        _countBeforeOlder += _olderCount;
        _olderCount = _newerCount;
        _newerCount = 0;
    }
}
