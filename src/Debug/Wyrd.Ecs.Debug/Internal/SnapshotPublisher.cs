namespace Wyrd.Ecs.Debug.Internal;

/// <summary>
/// Publishes a <see cref="WorldSnapshot"/> once per tick, gated by whether anyone is
/// connected. Deliberately not a registered <c>EcsSystem</c>: a full-world walk declares
/// no query access, so the parallel scheduler has no way to know it needs to run
/// exclusive of every other system; registering it as one would risk it running
/// concurrently with a mid-tick mutation, exactly the hazard
/// <see cref="World.EnumerateArchetypes()"/>/<see cref="World.EnumerateEntities()"/>'s own
/// eager materialization exists to avoid. <see cref="World.OnTickAdvanced"/> is already
/// the safe point that guarantee assumes: after every system for the tick has run and
/// commands are applied, before the next tick's mutations start.
/// </summary>
internal sealed class SnapshotPublisher(World world, CodecRegistry registry)
{
    private WorldSnapshot? _latest;
    private int _connectedCount;

    public WorldSnapshot? Latest => _latest;

    public void Connect() => Interlocked.Increment(ref _connectedCount);
    public void Disconnect() => Interlocked.Decrement(ref _connectedCount);

    public void OnTickAdvanced(int tick)
    {
        if (Volatile.Read(ref _connectedCount) == 0) return;

        var snapshot = new WorldSnapshot(world.EnumerateArchetypes(), world.EnumerateEntities(registry));
        Interlocked.Exchange(ref _latest, snapshot);
    }
}
