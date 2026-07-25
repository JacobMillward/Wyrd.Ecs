namespace Wyrd.Ecs;

/// <summary>
/// The only way to perform structural mutation — creating or destroying an entity,
/// adding or removing a component or tag. Queued operations are not visible until
/// <see cref="World.ApplyCommands()"/> runs: <c>HasComponent</c>/<c>GetComponent</c>
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
///
/// <para>
/// Nothing on this class is synchronized — every field below is private, per-instance
/// state. That's deliberate: a <see cref="CommandBuffer"/> instance is meant to have
/// exactly one writer. Concurrent structural mutation from several sources doesn't need
/// this class to gain locks; it needs each source to hold its own buffer, obtained via
/// <see cref="IWorld.CreateCommands"/>, and to have all of them applied — in whatever
/// order the caller chooses — via <see cref="IWorld.ApplyCommands(CommandBuffer)"/>. See
/// that method's docs for why a caller-chosen apply order, not an internally-synchronized
/// shared queue, is the mechanism this engine uses for safe concurrent command queuing.
/// </para>
/// </summary>
public sealed partial class CommandBuffer
{
    private readonly World _world;
    private QueuedCommand[] _queue = new QueuedCommand[4];
    private int _count;

    /// <summary>
    /// Guards every enqueue-side mutation (<see cref="_queue"/>/<see cref="_count"/>,
    /// <see cref="_addComponentBuffers"/>, and each <see cref="AddComponentBuffer{T}"/>'s
    /// own <c>Items</c>/<c>Count</c>) so several systems in the same
    /// <c>ScheduledExecutor</c> stage can call <see cref="AddComponent{T}"/>/
    /// <see cref="RemoveComponent{T}"/>/etc. against this same shared <c>world.Commands</c>
    /// buffer concurrently. Every public method below takes this lock for its entire
    /// body in one shot, rather than <see cref="Enqueue"/>/<see cref="GetAddComponentBuffer{T}"/>
    /// locking themselves — those two are plain unlocked helpers, always called with
    /// <see cref="_gate"/> already held, so a method needing both (<see cref="AddComponent{T}"/>)
    /// never has to acquire it twice.
    /// </summary>
    private readonly Lock _gate = new();

    internal CommandBuffer(World world) => _world = world;

    /// <summary>The <see cref="World"/> this buffer was created for — checked by <see cref="Wyrd.Ecs.World.ApplyCommands(CommandBuffer)"/> before replaying it.</summary>
    internal World World => _world;

    /// <summary>
    /// A raw growable array with a manual count, not <c>List&lt;QueuedCommand&gt;</c> —
    /// matches every other hot-path collection in this engine (<c>Archetype</c>'s rows,
    /// <c>ComponentStorage&lt;T&gt;</c>'s columns, <c>EntityTable</c>'s parallel arrays),
    /// none of which use <c>List&lt;T&gt;</c>. <c>List&lt;T&gt;</c> bumps a version counter
    /// on every <c>Add</c>/<c>Clear</c> and checks it on every enumerator <c>MoveNext</c>,
    /// to detect mutation during enumeration — a safety check this queue never needs,
    /// since nothing ever enumerates it while it's still being built. Caller must
    /// already hold <see cref="_gate"/> — see the field's own doc.
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
    /// this used to carry directly, which required boxing <c>T</c>
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
    /// <see cref="CommandBuffer"/> actually has, than the box it was meant to avoid. This
    /// sidesteps that whole tradeoff: nothing is pooled, so nothing needs its own
    /// synchronization beyond <see cref="_gate"/>, which every caller reaching this
    /// type already holds (see <see cref="AddComponent{T}"/>).
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

    /// <summary>Caller must already hold <see cref="_gate"/> — see the field's own doc.</summary>
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
        // TODO: once logging exists, warn here when the overwrite branch runs -
        // it's valid (last-queued value wins, matching every other op's
        // already-in-that-state-is-fine stance) but usually signals two systems
        // queuing AddComponent for the same entity without coordinating.
        internal static readonly Action<World, Entity, object?, int> Apply = (w, e, buffer, slot) =>
        {
            if (!w.IsAlive(e)) return;
            var value = ((AddComponentBuffer<T>)buffer!).Items[slot];
            if (w.HasComponent<T>(e))
                w.GetComponent<T>(e) = value;
            else
                w.AddComponent<T>(e) = value;
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
    /// <see cref="World.ApplyCommands()"/> runs. Safe to call concurrently from several
    /// threads at once (<see cref="World.ReserveEntity"/> is itself lock-free; only the
    /// queueing that follows needs <see cref="_gate"/>).
    /// </summary>
    public Entity CreateEntity()
    {
        var entity = _world.ReserveEntity();
        lock (_gate) Enqueue(new QueuedCommand(entity, PlaceReservedOp.Apply, null, 0));
        return entity;
    }

    /// <summary>Queues destroying <paramref name="entity"/>. A no-op at apply time if the entity was already destroyed (or never placed) by an earlier queued command. Safe to call concurrently from several threads at once.</summary>
    public void DestroyEntity(Entity entity)
    {
        lock (_gate) Enqueue(new QueuedCommand(entity, DestroyEntityOp.Apply, null, 0));
    }

    /// <summary>
    /// Queues adding <paramref name="value"/> to <paramref name="entity"/>. A no-op at
    /// apply time if the entity was destroyed by an earlier queued command. If
    /// <paramref name="entity"/> already has a <typeparamref name="T"/> by the time this
    /// command runs (an earlier queued <see cref="AddComponent{T}"/> for the same entity,
    /// or one from a previous batch that was never removed), this overwrites it instead
    /// of adding a second one — last-queued value wins, the same
    /// already-in-that-state-is-fine stance every other queued operation on this class
    /// takes. Safe to call concurrently from several threads at once.
    /// </summary>
    public void AddComponent<T>(Entity entity, T value) where T : struct, IComponent
    {
        lock (_gate)
        {
            var buffer = GetAddComponentBuffer<T>();
            Internal.ArrayGrowth.EnsureCapacity(ref buffer.Items, buffer.Count + 1);
            var slot = buffer.Count++;
            buffer.Items[slot] = value;
            Enqueue(new QueuedCommand(entity, AddComponentOp<T>.Apply, buffer, slot));
        }
    }

    /// <summary>Queues removing <typeparamref name="T"/> from <paramref name="entity"/>. A no-op at apply time if the entity was destroyed by an earlier queued command. Safe to call concurrently from several threads at once.</summary>
    public void RemoveComponent<T>(Entity entity) where T : struct, IComponent
    {
        lock (_gate) Enqueue(new QueuedCommand(entity, RemoveComponentOp<T>.Apply, null, 0));
    }

    /// <summary>Queues adding tag <typeparamref name="T"/> to <paramref name="entity"/>. A no-op at apply time if the entity was destroyed by an earlier queued command. Safe to call concurrently from several threads at once.</summary>
    public void AddTag<T>(Entity entity) where T : struct, ITag
    {
        lock (_gate) Enqueue(new QueuedCommand(entity, AddTagOp<T>.Apply, null, 0));
    }

    /// <summary>Queues removing tag <typeparamref name="T"/> from <paramref name="entity"/>. A no-op at apply time if the entity was destroyed by an earlier queued command. Safe to call concurrently from several threads at once.</summary>
    public void RemoveTag<T>(Entity entity) where T : struct, ITag
    {
        lock (_gate) Enqueue(new QueuedCommand(entity, RemoveTagOp<T>.Apply, null, 0));
    }

    /// <summary>
    /// Applies every queued command, in the order it was queued, then clears the queue.
    /// Each command re-checks <see cref="World.IsAlive"/> at its own point in the
    /// sequence (a just-created entity's placement always runs before any command
    /// queued after it, so chaining is safe), so an earlier command that destroys an
    /// entity silently invalidates any later command in the same batch still targeting
    /// it, rather than throwing — every queued operation on this class takes that same
    /// already-in-that-state-is-fine stance, so nothing on this class's own logic throws
    /// here in normal use. What can still throw is arbitrary consumer code reached
    /// through a structural-change notification (an <see cref="IStructuralChangeObserver"/>
    /// implementation this library doesn't control). The cleanup (clearing the queue,
    /// resetting the per-type add-component buffers) runs in a <c>finally</c> regardless,
    /// so a misbehaving observer never leaves the batch half-applied to be silently
    /// replayed by the next call to <see cref="Apply"/>. Only ever called single-threaded,
    /// from a stage's join point after every enqueueing thread has already returned — the
    /// replay loop itself doesn't need <see cref="_gate"/>, but the cleanup still takes it
    /// defensively, so a caller invoking <see cref="World.ApplyCommands()"/> while another
    /// thread is still enqueueing serializes safely instead of corrupting state (a misuse
    /// this lock happens to also catch, not a scenario the join-point discipline should
    /// ever actually produce).
    /// </summary>
    internal void Apply()
    {
        try
        {
            for (var i = 0; i < _count; i++)
            {
                ref readonly var command = ref _queue[i];
                command.Apply(_world, command.Entity, command.Buffer, command.Slot);
            }
        }
        finally
        {
            lock (_gate)
            {
                Array.Clear(_queue, 0, _count);
                _count = 0;

                foreach (var buffer in _addComponentBuffers)
                    (buffer as IResettableBuffer)?.ResetForNextBatch();
            }
        }
    }
}
