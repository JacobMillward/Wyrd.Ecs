namespace Wyrd.Ecs;

/// <summary>
/// A registered reader of one component type's change log. Registering is the only
/// way to observe <typeparamref name="T"/>'s changes: it is what turns tracking on for
/// <typeparamref name="T"/> (see <see cref="World.RegisterChangeConsumer{T}"/>), and
/// this consumer's own advanced position is what retention uses to decide what is
/// safe to trim.
/// </summary>
public sealed class ChangeConsumer<T> : Internal.IChangeConsumerHandle, IDisposable where T : struct, IComponent
{
    private readonly World _world;
    private readonly int _typeIndex;
    private int _tick;
    private bool _disposed;

    internal ChangeConsumer(World world, int typeIndex, int tick)
    {
        _world = world;
        _typeIndex = typeIndex;
        _tick = tick;
    }

    int Internal.IChangeConsumerHandle.Tick => _tick;

    /// <summary>Reads every change recorded after this consumer's current position, non-destructively.</summary>
    public ChangeReadQuery<T> ReadChanges()
    {
        ThrowIfDisposed();
        return new ChangeReadQuery<T>(_world.Archetypes, _typeIndex, _tick);
    }

    /// <summary>
    /// Marks everything up to and including <paramref name="tick"/> as durably handled.
    /// Only entries at or before the minimum tick across every live consumer of
    /// <typeparamref name="T"/> are eligible for retention to trim, so this call is
    /// what actually lets old entries go.
    /// </summary>
    public void Advance(int tick)
    {
        ThrowIfDisposed();
        if (tick < _tick || tick > _world.CurrentTick)
        {
            throw new ArgumentOutOfRangeException(nameof(tick), tick,
                $"Must be between this consumer's current position ({_tick}) and the world's current tick ({_world.CurrentTick}).");
        }

        _tick = tick;
    }

    /// <summary>
    /// Unregisters this consumer. Immediately unblocks retention for
    /// <typeparamref name="T"/>, and if this was the last live consumer for
    /// <typeparamref name="T"/>, turns tracking for it back off.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _world.UnregisterChangeConsumer(_typeIndex, this);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ChangeConsumer<T>));
    }
}
