namespace Wyrd.Ecs;

/// <summary>
/// A live subscription to a <see cref="World"/>'s changes, obtained from
/// <see cref="World.Subscribe{T}"/>. Owns a private buffer nothing else reads,
/// trims, or waits on — call <see cref="Drain"/> at whatever pace suits this
/// subscriber, from any thread, independently of every other subscription on the
/// same <see cref="World"/>. There is no shared retained log behind this: each
/// subscription's buffer is populated by one shared scan per watched type per tick,
/// then holds nothing once drained. Dispose to stop receiving further changes.
/// </summary>
public sealed class ChangeSubscription : IDisposable
{
    private readonly Internal.ChangeFeedHub _hub;
    private readonly int _id;
    private bool _disposed;

    internal ChangeSubscription(Internal.ChangeFeedHub hub, int id)
    {
        _hub = hub;
        _id = id;
    }

    /// <summary>Every change recorded since the last call (or since subscribing, for the first call). Clears the buffer.</summary>
    public IReadOnlyList<ChangeEntry> Drain() => _hub.Drain(_id);

    /// <summary>Stops this subscription — no further changes are reported, and its buffer is released.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _hub.Unsubscribe(_id);
    }
}
