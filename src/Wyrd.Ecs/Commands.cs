namespace Wyrd.Ecs;

/// <summary>
/// The only way to perform structural mutation — creating or destroying an entity,
/// adding or removing a component or tag. Queued operations are not visible until
/// <see cref="World.ApplyCommands"/> runs: <c>HasComponent</c>/<c>GetComponent</c>
/// called against a queued-but-not-yet-applied change still reflect pre-apply state,
/// and an entity created here is not <see cref="World.IsAlive"/> until then either. This
/// exists for two reasons: performing a structural change on an entity while a
/// <see cref="IWorld"/> query is iterating the same archetype mutates the same backing
/// arrays the enumerator is mid-walk over, with no guard — queuing through here and
/// applying afterward avoids that; and a structural change touches world-level shared
/// state (the archetype graph, the entity table) that no future per-component
/// parallel-scheduling access-conflict graph can reason about, so deferring to one
/// single-threaded apply point is a hard prerequisite for running systems concurrently.
/// There is deliberately no direct, immediate alternative on <see cref="IWorld"/> — with
/// one only reachable through discipline, not the type system, a caller could still
/// trigger the exact hazard this exists to prevent by picking the wrong one. Reading and
/// mutating an already-placed entity's existing component values
/// (<see cref="World.GetComponent{T}"/> and friends) never touches archetype row
/// layout, carries no such hazard, and stays direct on <see cref="IWorld"/> — this class
/// is only ever about changing an entity's shape, never about its values.
/// </summary>
public sealed partial class Commands
{
    private readonly World _world;
    private readonly List<Action<World>> _queue = new();

    internal Commands(World world) => _world = world;

    /// <summary>
    /// Reserves a real <see cref="Entity"/> immediately (so it can be used to chain
    /// further commands in the same batch) and queues its placement into the world.
    /// The returned entity is not <see cref="World.IsAlive"/> until
    /// <see cref="World.ApplyCommands"/> runs.
    /// </summary>
    public Entity CreateEntity()
    {
        var entity = _world.ReserveEntity();
        _queue.Add(w => w.PlaceReservedEntity(entity));
        return entity;
    }

    /// <summary>Queues destroying <paramref name="entity"/>. A no-op at apply time if the entity was already destroyed (or never placed) by an earlier queued command.</summary>
    public void DestroyEntity(Entity entity) =>
        _queue.Add(w => { if (w.IsAlive(entity)) w.DestroyEntity(entity); });

    /// <summary>Queues adding <paramref name="value"/> to <paramref name="entity"/>. A no-op at apply time if the entity was destroyed by an earlier queued command.</summary>
    public void AddComponent<T>(Entity entity, T value) where T : struct, IComponent =>
        _queue.Add(w => { if (w.IsAlive(entity)) w.AddComponent<T>(entity) = value; });

    /// <summary>Queues removing <typeparamref name="T"/> from <paramref name="entity"/>. A no-op at apply time if the entity was destroyed by an earlier queued command.</summary>
    public void RemoveComponent<T>(Entity entity) where T : struct, IComponent =>
        _queue.Add(w => { if (w.IsAlive(entity)) w.RemoveComponent<T>(entity); });

    /// <summary>Queues adding tag <typeparamref name="T"/> to <paramref name="entity"/>. A no-op at apply time if the entity was destroyed by an earlier queued command.</summary>
    public void AddTag<T>(Entity entity) where T : struct, ITag =>
        _queue.Add(w => { if (w.IsAlive(entity)) w.AddTag<T>(entity); });

    /// <summary>Queues removing tag <typeparamref name="T"/> from <paramref name="entity"/>. A no-op at apply time if the entity was destroyed by an earlier queued command.</summary>
    public void RemoveTag<T>(Entity entity) where T : struct, ITag =>
        _queue.Add(w => { if (w.IsAlive(entity)) w.RemoveTag<T>(entity); });

    /// <summary>
    /// Applies every queued command, in the order it was queued, then clears the queue.
    /// Each command re-checks <see cref="World.IsAlive"/> at its own point in the
    /// sequence (a just-created entity's placement always runs before any command
    /// queued after it, so chaining is safe), so an earlier command that destroys an
    /// entity silently invalidates any later command in the same batch still targeting
    /// it, rather than throwing.
    /// </summary>
    internal void Apply()
    {
        foreach (var command in _queue)
            command(_world);
        _queue.Clear();
    }
}
