namespace Wyrd.Ecs;

/// <summary>
/// A live subscription to a <see cref="World"/>'s changes, obtained from
/// <see cref="World.Subscribe{T}"/>. Call <see cref="Drain"/> at whatever pace suits
/// this subscriber, from any thread, independently of every other subscription on the
/// same <see cref="World"/>. Dispose to stop receiving further changes.
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

    /// <summary>Stops this subscription: no further changes are reported, and its buffer is released.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _hub.Unsubscribe(_id);
    }
}
