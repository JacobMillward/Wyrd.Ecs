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
    private QueuedCommand[] _queue = new QueuedCommand[4];
    private int _count;

    internal Commands(World world) => _world = world;

    /// <summary>
    /// A raw growable array with a manual count, not <c>List&lt;QueuedCommand&gt;</c> —
    /// matches every other hot-path collection in this engine (<c>Archetype</c>'s rows,
    /// <c>ComponentStorage&lt;T&gt;</c>'s columns, <c>EntityTable</c>'s parallel arrays),
    /// none of which use <c>List&lt;T&gt;</c>. <c>List&lt;T&gt;</c> bumps a version counter
    /// on every <c>Add</c>/<c>Clear</c> and checks it on every enumerator <c>MoveNext</c>,
    /// to detect mutation during enumeration — a safety check this queue never needs,
    /// since nothing ever enumerates it while it's still being built.
    /// </summary>
    private void Enqueue(QueuedCommand command)
    {
        Internal.ArrayGrowth.EnsureCapacity(ref _queue, _count + 1);
        _queue[_count++] = command;
    }

    /// <summary>
    /// One queued operation: the target entity, a cached non-capturing dispatcher
    /// delegate (one static instance per closed generic operation, shared across every
    /// call rather than allocated per call), and — for <see cref="AddComponent{T}"/>
    /// only — a reference to that component type's <see cref="AddComponentBuffer{T}"/>
    /// plus the slot within it. Every other operation leaves <see cref="Buffer"/> null
    /// and <see cref="Slot"/> 0; their dispatcher delegates ignore both. Passing a
    /// buffer reference costs nothing (it's already a heap object), unlike the value
    /// this used to carry directly, which required boxing <typeparamref name="T"/>
    /// fresh on every single call — see <see cref="AddComponentBuffer{T}"/>'s docs for
    /// why storing the value there instead removes that allocation entirely.
    /// </summary>
    private readonly struct QueuedCommand(Entity entity, Action<World, Entity, object?, int> apply, object? buffer, int slot)
    {
        internal readonly Entity Entity = entity;
        internal readonly Action<World, Entity, object?, int> Apply = apply;
        internal readonly object? Buffer = buffer;
        internal readonly int Slot = slot;
    }

    /// <summary>Non-generic hook so <see cref="Apply"/> can reset every touched <see cref="AddComponentBuffer{T}"/> back to empty without needing to know its <c>T</c>.</summary>
    private interface IResettableBuffer
    {
        void ResetForNextBatch();
    }

    /// <summary>
    /// One component type's queued <see cref="AddComponent{T}"/> values, stored as a
    /// real <typeparamref name="T"/>[] — the same struct-of-arrays shape
    /// <c>ComponentStorage&lt;T&gt;</c> already uses for archetype columns, and for the
    /// same reason: the *container* reference is type-erased (indexed by
    /// <see cref="Internal.TypeIndex{T}"/> in <see cref="_addComponentBuffers"/>, held as
    /// <c>object</c>), but the *payload* is never boxed, because it lives in a genuinely
    /// generic array. A pooling scheme was measured and rejected here (see the pooling
    /// benchmarks) — every thread-safe pool tried cost more, in the access pattern
    /// <see cref="Commands"/> actually has, than the box it was meant to avoid. This
    /// sidesteps that whole tradeoff: nothing is pooled or shared, so nothing needs
    /// synchronization — it's exactly as single-writer as <see cref="_queue"/> already is.
    /// Reset to empty at the end of every <see cref="Apply"/> (its backing array is kept,
    /// not reallocated, same as <see cref="_queue"/>'s own <c>Array.Clear</c> pattern).
    /// </summary>
    private sealed class AddComponentBuffer<T> : IResettableBuffer where T : struct, IComponent
    {
        internal T[] Items = new T[4];
        internal int Count;

        public void ResetForNextBatch() => Count = 0;
    }

    private object?[] _addComponentBuffers = new object?[4];

    private AddComponentBuffer<T> GetAddComponentBuffer<T>() where T : struct, IComponent
    {
        var typeIndex = Internal.TypeIndex<T>.Value;
        Internal.ArrayGrowth.EnsureCapacity(ref _addComponentBuffers, typeIndex + 1);
        if (_addComponentBuffers[typeIndex] is AddComponentBuffer<T> existing) return existing;

        var created = new AddComponentBuffer<T>();
        _addComponentBuffers[typeIndex] = created;
        return created;
    }

    private static class DestroyEntityOp
    {
        internal static readonly Action<World, Entity, object?, int> Apply = (w, e, _, _) => { if (w.IsAlive(e)) w.DestroyEntity(e); };
    }

    private static class PlaceReservedOp
    {
        internal static readonly Action<World, Entity, object?, int> Apply = (w, e, _, _) => w.PlaceReservedEntity(e);
    }

    private static class AddComponentOp<T> where T : struct, IComponent
    {
        internal static readonly Action<World, Entity, object?, int> Apply = (w, e, buffer, slot) =>
        {
            if (w.IsAlive(e)) w.AddComponent<T>(e) = ((AddComponentBuffer<T>)buffer!).Items[slot];
        };
    }

    private static class RemoveComponentOp<T> where T : struct, IComponent
    {
        internal static readonly Action<World, Entity, object?, int> Apply = (w, e, _, _) => { if (w.IsAlive(e)) w.RemoveComponent(e, Internal.TypeIndex<T>.Value); };
    }

    private static class AddTagOp<T> where T : struct, ITag
    {
        internal static readonly Action<World, Entity, object?, int> Apply = (w, e, _, _) => { if (w.IsAlive(e)) w.AddTag(e, Internal.TypeIndex<T>.Value); };
    }

    private static class RemoveTagOp<T> where T : struct, ITag
    {
        internal static readonly Action<World, Entity, object?, int> Apply = (w, e, _, _) => { if (w.IsAlive(e)) w.RemoveTag(e, Internal.TypeIndex<T>.Value); };
    }

    /// <summary>
    /// Reserves a real <see cref="Entity"/> immediately (so it can be used to chain
    /// further commands in the same batch) and queues its placement into the world.
    /// The returned entity is not <see cref="World.IsAlive"/> until
    /// <see cref="World.ApplyCommands"/> runs.
    /// </summary>
    public Entity CreateEntity()
    {
        var entity = _world.ReserveEntity();
        Enqueue(new QueuedCommand(entity, PlaceReservedOp.Apply, null, 0));
        return entity;
    }

    /// <summary>Queues destroying <paramref name="entity"/>. A no-op at apply time if the entity was already destroyed (or never placed) by an earlier queued command.</summary>
    public void DestroyEntity(Entity entity) =>
        Enqueue(new QueuedCommand(entity, DestroyEntityOp.Apply, null, 0));

    /// <summary>Queues adding <paramref name="value"/> to <paramref name="entity"/>. A no-op at apply time if the entity was destroyed by an earlier queued command.</summary>
    public void AddComponent<T>(Entity entity, T value) where T : struct, IComponent
    {
        var buffer = GetAddComponentBuffer<T>();
        Internal.ArrayGrowth.EnsureCapacity(ref buffer.Items, buffer.Count + 1);
        var slot = buffer.Count++;
        buffer.Items[slot] = value;
        Enqueue(new QueuedCommand(entity, AddComponentOp<T>.Apply, buffer, slot));
    }

    /// <summary>Queues removing <typeparamref name="T"/> from <paramref name="entity"/>. A no-op at apply time if the entity was destroyed by an earlier queued command.</summary>
    public void RemoveComponent<T>(Entity entity) where T : struct, IComponent =>
        Enqueue(new QueuedCommand(entity, RemoveComponentOp<T>.Apply, null, 0));

    /// <summary>Queues adding tag <typeparamref name="T"/> to <paramref name="entity"/>. A no-op at apply time if the entity was destroyed by an earlier queued command.</summary>
    public void AddTag<T>(Entity entity) where T : struct, ITag =>
        Enqueue(new QueuedCommand(entity, AddTagOp<T>.Apply, null, 0));

    /// <summary>Queues removing tag <typeparamref name="T"/> from <paramref name="entity"/>. A no-op at apply time if the entity was destroyed by an earlier queued command.</summary>
    public void RemoveTag<T>(Entity entity) where T : struct, ITag =>
        Enqueue(new QueuedCommand(entity, RemoveTagOp<T>.Apply, null, 0));

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
        for (var i = 0; i < _count; i++)
        {
            ref readonly var command = ref _queue[i];
            command.Apply(_world, command.Entity, command.Buffer, command.Slot);
        }

        Array.Clear(_queue, 0, _count);
        _count = 0;

        foreach (var buffer in _addComponentBuffers)
            (buffer as IResettableBuffer)?.ResetForNextBatch();
    }
}
