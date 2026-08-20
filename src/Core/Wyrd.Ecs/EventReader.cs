namespace Wyrd.Ecs;

/// <summary>
/// Drains one event type's channel at whatever pace this reader calls <see cref="Read"/>,
/// independently of every other reader on the same <see cref="World"/>. Obtained from
/// <see cref="World.CreateEventReader{T}"/>. Store one as a field on whichever system reads
/// this event type and call <see cref="Read"/> once every tick, with no gaps: an emission is
/// retained across exactly two tick boundaries, so a reader that skips a tick between calls
/// can silently miss whatever fell out of the window in between. Not <see cref="IDisposable"/>:
/// unlike <see cref="ChangeSubscription"/>, there's no per-reader state on the channel itself
/// to release - letting this go out of scope is enough.
/// </summary>
public sealed class EventReader<T> where T : struct, IEvent
{
    private readonly Internal.EventChannel<T> _channel;
    private readonly List<T> _buffer = [];
    private long _cursor;

    internal EventReader(Internal.EventChannel<T> channel)
    {
        _channel = channel;
        _cursor = channel.SnapshotCursor();
    }

    /// <summary>Every event written since the last call (or since this reader was created, for the first call). The returned list is reused across calls - copy anything you need to keep past the next call.</summary>
    public IReadOnlyList<T> Read()
    {
        _cursor = _channel.Read(_cursor, _buffer);
        return _buffer;
    }
}
