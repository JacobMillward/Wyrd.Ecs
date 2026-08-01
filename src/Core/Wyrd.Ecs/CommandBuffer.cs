namespace Wyrd.Ecs;

/// <summary>
/// The only way to perform structural mutation: creating or destroying an entity, adding
/// or removing a component or tag. Queued operations are not visible until
/// <see cref="World.ApplyCommands()"/> runs. Deferred rather than immediate for two
/// reasons: mutating an archetype's backing arrays while a query is mid-walk over the
/// same archetype has no guard against corruption, and structural mutation touches
/// world-level shared state that a per-component parallel scheduler can't reason about,
/// so a single-threaded apply point is required for running systems concurrently at all.
/// Reading and mutating an already-placed entity's existing values
/// (<see cref="World.GetComponent{T}(Entity)"/> and friends) never touches archetype row
/// layout, so it stays direct on <see cref="World"/> instead.
///
/// <para>
/// Nothing here is synchronized: every field is private, per-instance state, since a
/// <see cref="CommandBuffer"/> is meant to have exactly one writer. Several concurrent
/// sources queue safely by each holding their own buffer (<see cref="World.CreateCommands"/>),
/// then applying them all, in whatever order the caller chooses, via
/// <see cref="World.ApplyCommands(CommandBuffer)"/>.
/// </para>
/// </summary>
public sealed partial class CommandBuffer
{
    private readonly World _world;
    private QueuedCommand[] _queue = new QueuedCommand[4];
    private int _count;

    /// <summary>
    /// Guards every enqueue-side mutation (<see cref="_queue"/>/<see cref="_count"/>,
    /// <see cref="_addComponentBuffers"/>, and each buffer's own <c>Items</c>/<c>Count</c>)
    /// so several systems in the same stage can queue against this shared buffer
    /// concurrently. Every public method locks once for its whole body; <see cref="Enqueue"/>
    /// and the buffer getters are unlocked helpers always called with this already held, so
    /// a method needing both never acquires it twice.
    /// </summary>
    private readonly Lock _gate = new();

    internal CommandBuffer(World world) => _world = world;

    /// <summary>The <see cref="World"/> this buffer was created for. Checked by <see cref="Wyrd.Ecs.World.ApplyCommands(CommandBuffer)"/> before replaying it.</summary>
    internal World World => _world;

    /// <summary>
    /// Appends to a raw growable array with a manual count, not <c>List&lt;QueuedCommand&gt;</c>,
    /// matching every other hot-path collection in this engine. <c>List&lt;T&gt;</c> bumps a
    /// version counter on every <c>Add</c>/<c>Clear</c> to detect mutation during enumeration,
    /// a check this queue never needs since nothing enumerates it while still being built.
    /// Caller must already hold <see cref="_gate"/>.
    /// </summary>
    private void Enqueue(QueuedCommand command)
    {
        Internal.ArrayGrowth.EnsureCapacity(ref _queue, _count + 1);
        _queue[_count++] = command;
    }

    /// <summary>
    /// One queued operation: the target entity, a cached non-capturing dispatcher delegate
    /// (one static instance per closed generic operation, shared across every call), and,
    /// only for <see cref="World.AddComponent{T}(Entity)"/>, a reference to that component
    /// type's <see cref="AddComponentBuffer{T}"/> plus the slot within it. Every other
    /// operation leaves <see cref="Buffer"/> null and <see cref="Slot"/> 0. Passing a buffer
    /// reference costs nothing, since it's already a heap object; see
    /// <see cref="AddComponentBuffer{T}"/> for why the value itself is never boxed.
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
    /// One component type's queued <see cref="World.AddComponent{T}(Entity)"/> values, stored
    /// as a real <typeparamref name="T"/>[]: the container reference is type-erased (indexed
    /// by <see cref="Internal.TypeIndex{T}"/> in <see cref="_addComponentBuffers"/>, held as
    /// <c>object</c>), but the payload is never boxed, since it lives in a genuinely generic
    /// array. A pooling scheme was measured and rejected: every thread-safe pool tried cost
    /// more, in this access pattern, than the box it was meant to avoid. Needs no
    /// synchronization beyond <see cref="_gate"/>, which every caller already holds. Reset to
    /// empty at the end of every <see cref="Apply"/>; its backing array is kept, not
    /// reallocated.
    /// </summary>
    private sealed class AddComponentBuffer<T> : IResettableBuffer where T : struct, IComponent
    {
        internal T[] Items = new T[4];
        internal int Count;

        public void ResetForNextBatch() => Count = 0;
    }

    private object?[] _addComponentBuffers = new object?[4];

    /// <summary>One relation type's queued <see cref="AddRelation{T}(Entity, Entity, T)"/> values, as a <c>(Entity Target, T Value)[]</c>: same reasoning as <see cref="AddComponentBuffer{T}"/>, the value payload is never boxed, only the container reference is type-erased.</summary>
    private sealed class AddRelationBuffer<T> : IResettableBuffer where T : struct, IRelation
    {
        internal (Entity Target, T Value)[] Items = new (Entity, T)[4];
        internal int Count;

        public void ResetForNextBatch() => Count = 0;
    }

    private object?[] _addRelationBuffers = new object?[4];

    /// <summary>Caller must already hold <see cref="_gate"/>; see <see cref="GetAddComponentBuffer{T}"/> for why.</summary>
    private AddRelationBuffer<T> GetAddRelationBuffer<T>() where T : struct, IRelation
    {
        var typeIndex = Internal.TypeIndex<T>.Value;
        Internal.ArrayGrowth.EnsureCapacity(ref _addRelationBuffers, typeIndex + 1);
        if (_addRelationBuffers[typeIndex] is AddRelationBuffer<T> existing) return existing;

        var created = new AddRelationBuffer<T>();
        _addRelationBuffers[typeIndex] = created;
        return created;
    }

    /// <summary>
    /// The queued-target buffer for every <see cref="RemoveRelation{T}"/> call, shared across
    /// every relation type: removal carries no per-edge payload, only a target
    /// <see cref="Entity"/>, so unlike <see cref="AddRelationBuffer{T}"/> there's no value to
    /// keep unboxed per closed generic. Each queued command still captures its own
    /// <c>(buffer, slot)</c> pair at enqueue time, so sharing this array across relation
    /// types is as safe as <see cref="_queue"/> being shared across every command kind.
    /// </summary>
    private sealed class RelationTargetBuffer : IResettableBuffer
    {
        internal Entity[] Items = new Entity[4];
        internal int Count;

        public void ResetForNextBatch() => Count = 0;
    }

    private RelationTargetBuffer? _relationTargetBuffer;

    /// <summary>Caller must already hold <see cref="_gate"/>.</summary>
    private RelationTargetBuffer GetRelationTargetBuffer() => _relationTargetBuffer ??= new RelationTargetBuffer();

    /// <summary>Caller must already hold <see cref="_gate"/>; see the field's own doc.</summary>
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

    private static class BatchPlaceReservedOp
    {
        internal static readonly Action<World, Entity, object?, int> Apply = (w, _, buffer, _) => w.PlaceReservedEntities((Entity[])buffer!);
    }

    private static class TemplatePlacementOp
    {
        internal static readonly Action<World, Entity, object?, int> Apply = (w, e, buffer, _) =>
        {
            var template = (EntityTemplate)buffer!;
            w.PlaceReservedEntityFromTemplate(e, template.Signature, template.Setters);
        };
    }

    private static class BatchTemplatePlacementOp
    {
        internal static readonly Action<World, Entity, object?, int> Apply = (w, _, buffer, _) =>
        {
            var (entities, template) = ((Entity[], EntityTemplate))buffer!;
            w.PlaceReservedEntitiesFromTemplate(entities, template.Signature, template.Setters);
        };
    }

    private static class AddComponentOp<T> where T : struct, IComponent
    {
        // TODO: once logging exists, warn here when the overwrite branch runs. It's valid
        // (last-queued value wins, matching every other op's already-in-that-state-is-fine
        // stance) but usually signals two systems queuing AddComponent for the same entity
        // without coordinating.
        internal static readonly Action<World, Entity, object?, int> Apply = (w, e, buffer, slot) =>
        {
            if (!w.TryResolve(e, out var location)) return;
            var value = ((AddComponentBuffer<T>)buffer!).Items[slot];
            var typeIndex = Internal.TypeIndex<T>.Value;
            if (location.Archetype.Signature.Contains(typeIndex))
                w.GetComponent<T>(e, location) = value;
            else
                w.AddComponent<T>(e, location) = value;
        };
    }

    private static class RemoveComponentOp<T> where T : struct, IComponent
    {
        internal static readonly Action<World, Entity, object?, int> Apply = (w, e, _, _) =>
        {
            if (w.TryResolve(e, out var location)) w.RemoveComponent(e, location, Internal.TypeIndex<T>.Value);
        };
    }

    private static class AddTagOp<T> where T : struct, ITag
    {
        internal static readonly Action<World, Entity, object?, int> Apply = (w, e, _, _) =>
        {
            if (w.TryResolve(e, out var location)) w.AddTag(e, location, Internal.TypeIndex<T>.Value);
        };
    }

    private static class RemoveTagOp<T> where T : struct, ITag
    {
        internal static readonly Action<World, Entity, object?, int> Apply = (w, e, _, _) =>
        {
            if (w.TryResolve(e, out var location)) w.RemoveTag(e, location, Internal.TypeIndex<T>.Value);
        };
    }

    private static class AddRelationOp<T> where T : struct, IRelation
    {
        internal static readonly Action<World, Entity, object?, int> Apply = (w, source, buffer, slot) =>
        {
            var (target, value) = ((AddRelationBuffer<T>)buffer!).Items[slot];
            if (!w.TryResolve(source, out _)) return;
            if (!w.TryResolve(target, out _)) return; // target must be alive too, checked before mutating anything

            if (Internal.RelationTraits<T>.IsExclusive)
                w.ReplaceExclusiveRelationTarget<T>(source, target);

            // Fresh resolve: ReplaceExclusiveRelationTarget, if it ran, may have moved source.
            if (!w.TryResolve(source, out var sourceLocation)) return;
            ref var links = ref w.GetOrCreateRelationLinks<T>(source, sourceLocation);
            links.Targets![target] = value;

            // Fresh resolve, not the location captured above: if source == target, the
            // write just above may itself have moved it (first edge on this relation type).
            if (!w.TryResolve(target, out var targetLocation)) return;
            ref var backlinks = ref w.GetOrCreateRelationBacklinks<T>(target, targetLocation);
            backlinks.Sources!.Add(source);
        };
    }

    private static class RemoveRelationOp<T> where T : struct, IRelation
    {
        internal static readonly Action<World, Entity, object?, int> Apply = (w, source, buffer, slot) =>
        {
            var target = ((RelationTargetBuffer)buffer!).Items[slot];
            w.RemoveRelationLink<T>(source, target);
            w.RemoveRelationBacklink<T>(target, source);
        };
    }

    /// <summary>
    /// Reserves a real <see cref="Entity"/> immediately and queues its placement into the
    /// world, returning an <see cref="EntityView"/> bound to this buffer so further calls
    /// can chain immediately: <c>commands.CreateEntity().AddComponent(pos).SetParent(p)</c>.
    /// Assign to an <see cref="Entity"/>-typed variable or parameter (e.g. <c>Entity e =
    /// commands.CreateEntity();</c>) to get the raw, storable id via <see cref="EntityView"/>'s
    /// implicit conversion. <c>var</c> instead infers <see cref="EntityView"/> itself. The
    /// entity is not <see cref="World.IsAlive"/> until <see cref="World.ApplyCommands()"/> runs. Safe
    /// to call concurrently from several threads at once (<see cref="World.ReserveEntity"/>
    /// is itself lock-free; only the queueing that follows needs <see cref="_gate"/>).
    /// </summary>
    public EntityView CreateEntity()
    {
        var entity = _world.ReserveEntity();
        lock (_gate) Enqueue(new QueuedCommand(entity, PlaceReservedOp.Apply, null, 0));
        return new EntityView(_world, this, entity);
    }

    /// <summary>
    /// Reserves a real <see cref="Entity"/> immediately and queues its placement directly
    /// into the archetype matching <paramref name="template"/>'s components/tags — the
    /// <see cref="EntityTemplate"/> counterpart of the generated
    /// <c>CreateEntity&lt;T0..Tn&gt;</c> family. For a template with children, instead
    /// reserves and places every node of its tree (see <see cref="CreateEntityFromTree"/>);
    /// a childless template with no <see cref="EntityTemplate.ExplicitParent"/> stays on
    /// <see cref="CreateEntitySingleNode"/>'s zero-extra-allocation path regardless of how
    /// it's called. Not <see cref="World.IsAlive"/> until <see cref="World.ApplyCommands()"/> runs.
    /// </summary>
    public EntityView CreateEntity(EntityTemplate template) =>
        template.Children.Count > 0 || template.ExplicitParent.HasValue
            ? CreateEntityFromTree(template)
            : CreateEntitySingleNode(template);

    private EntityView CreateEntitySingleNode(EntityTemplate template)
    {
        var entity = _world.ReserveEntity();
        lock (_gate) Enqueue(new QueuedCommand(entity, TemplatePlacementOp.Apply, template, 0));
        return new EntityView(_world, this, entity);
    }

    private static class TemplateNodePlacementOp
    {
        internal static readonly Action<World, Entity, object?, int> Apply = (w, e, buffer, _) =>
        {
            var (signature, setters) = ((Internal.ArchetypeSignature, IReadOnlyCollection<TemplateComponentSetter>))buffer!;
            w.PlaceReservedEntityFromTemplate(e, signature, setters);
        };
    }

    /// <summary>
    /// One node in a flattened <see cref="EntityTemplate"/> tree: the node's own template
    /// and the index, within the same flattened list, of its in-tree parent (-1 for the
    /// root). Built by <see cref="FlattenTemplate"/>.
    /// </summary>
    private readonly record struct TemplateTreeNode(EntityTemplate Template, int ParentIndex);

    /// <summary>
    /// Depth-first flattens <paramref name="template"/> and every descendant (via
    /// <see cref="EntityTemplate.Children"/>) into <paramref name="nodes"/>, each entry
    /// recording its in-tree parent's index within the same list.
    /// </summary>
    private static void FlattenTemplate(EntityTemplate template, int parentIndex, List<TemplateTreeNode> nodes)
    {
        var index = nodes.Count;
        nodes.Add(new TemplateTreeNode(template, parentIndex));
        foreach (var child in template.Children)
            FlattenTemplate(child, index, nodes);
    }

    /// <summary>Queues adding <c>child</c> to <c>parent</c>'s <see cref="RelationBacklinks{T}"/> — the "existing parent's side" half of <see cref="EntityTemplate.AddParent"/>'s cost, a no-op if <c>parent</c> is no longer alive by apply time.</summary>
    private static class ExplicitParentBacklinkOp
    {
        internal static readonly Action<World, Entity, object?, int> Apply = (w, _, buffer, _) =>
        {
            var (parent, child) = ((Entity, Entity))buffer!;
            if (!w.TryResolve(parent, out var location)) return;
            ref var backlinks = ref w.GetOrCreateRelationBacklinks<Parent>(parent, location);
            backlinks.Sources!.Add(child);
        };
    }

    /// <summary>
    /// Reserves a real <see cref="Entity"/> for the root, and one more for every descendant
    /// in <paramref name="template"/>'s child tree, in a single bulk
    /// <see cref="World.ReserveEntityRange"/> call — not one reservation per node — then
    /// queues one placement per node, each going directly into its own final archetype
    /// (its own components/tags, plus a <see cref="RelationLinks{T}"/>/<see cref="RelationBacklinks{T}"/>
    /// pair wherever an in-tree parent/child edge applies). Returns an
    /// <see cref="EntityView"/> bound to the root only — descendants are discoverable
    /// afterward via <see cref="World.Sources{T}"/>/<see cref="World.Targets{T}"/> once
    /// alive, same as any other <see cref="Parent"/> edge.
    /// </summary>
    private EntityView CreateEntityFromTree(EntityTemplate template)
    {
        var nodes = new List<TemplateTreeNode>();
        FlattenTemplate(template, -1, nodes);

        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].ParentIndex >= 0 && nodes[i].Template.ExplicitParent.HasValue)
                throw new InvalidOperationException("A template cannot have both an in-tree parent (via AddChild) and an explicit parent (via AddParent).");
        }

        var entities = new Entity[nodes.Count];
        _world.ReserveEntityRange(entities);

        // childrenOf[i]: every index j whose in-tree parent is node i. Needed up front so
        // a parent node's RelationBacklinks<Parent> can be pre-populated with every one of
        // its (already-reserved) children's ids before any placement is queued.
        var childrenOf = new List<int>[nodes.Count];
        for (var i = 0; i < nodes.Count; i++) childrenOf[i] = new List<int>();
        for (var i = 1; i < nodes.Count; i++) childrenOf[nodes[i].ParentIndex].Add(i);

        lock (_gate)
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                var signature = node.Template.Signature;
                var setters = new List<TemplateComponentSetter>(node.Template.Setters);

                // See EntityTemplate.AddParent's own remarks for why this check runs once,
                // now, rather than being deferred to ApplyCommands().
                Entity? explicitParentIfAlive = node.ParentIndex < 0 && node.Template.ExplicitParent is { } explicitParent && _world.IsAlive(explicitParent)
                    ? explicitParent
                    : null;

                if (node.ParentIndex >= 0)
                {
                    var parentEntity = entities[node.ParentIndex];
                    signature = signature.With(Internal.TypeIndex<RelationLinks<Parent>>.Value);
                    setters.Add((w, archetype, startRow, count) =>
                    {
                        var storage = archetype.GetOrCreateStorage<RelationLinks<Parent>>();
                        storage.Fill(startRow, count, new RelationLinks<Parent>(new Dictionary<Entity, Parent> { [parentEntity] = default }));
                    });
                }
                else if (explicitParentIfAlive is { } liveExplicitParent)
                {
                    signature = signature.With(Internal.TypeIndex<RelationLinks<Parent>>.Value);
                    setters.Add((w, archetype, startRow, count) =>
                    {
                        var storage = archetype.GetOrCreateStorage<RelationLinks<Parent>>();
                        storage.Fill(startRow, count, new RelationLinks<Parent>(new Dictionary<Entity, Parent> { [liveExplicitParent] = default }));
                    });
                }

                if (childrenOf[i].Count > 0)
                {
                    var childEntities = new HashSet<Entity>(childrenOf[i].Select(j => entities[j]));
                    signature = signature.With(Internal.TypeIndex<RelationBacklinks<Parent>>.Value);
                    setters.Add((w, archetype, startRow, count) =>
                    {
                        var storage = archetype.GetOrCreateStorage<RelationBacklinks<Parent>>();
                        storage.Fill(startRow, count, new RelationBacklinks<Parent>(childEntities));
                    });
                }

                Enqueue(new QueuedCommand(entities[i], TemplateNodePlacementOp.Apply, (signature, (IReadOnlyCollection<TemplateComponentSetter>)setters), 0));

                if (explicitParentIfAlive is { } parentToBacklink)
                    Enqueue(new QueuedCommand(default, ExplicitParentBacklinkOp.Apply, (parentToBacklink, entities[i]), 0));
            }
        }

        return new EntityView(_world, this, entities[0]);
    }

    /// <summary>
    /// Bulk counterpart to <see cref="CreateEntity()"/>: reserves <paramref name="count"/>
    /// real <see cref="Entity"/> ids immediately via <see cref="World.ReserveEntityRange"/>
    /// (one bulk reservation, not <paramref name="count"/> individual ones) and queues
    /// their placement into the empty archetype as a single deferred command. The
    /// returned entities are not <see cref="World.IsAlive"/> until
    /// <see cref="World.ApplyCommands()"/> runs. Returns <see cref="Array.Empty{T}"/> for
    /// <paramref name="count"/> == 0 without reserving or queuing anything; throws
    /// <see cref="ArgumentOutOfRangeException"/> for a negative count.
    /// </summary>
    public Entity[] CreateEntity(int count)
    {
        if (count == 0) return Array.Empty<Entity>();
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), count, "count must be non-negative.");

        var entities = new Entity[count];
        _world.ReserveEntityRange(entities);

        lock (_gate) Enqueue(new QueuedCommand(default, BatchPlaceReservedOp.Apply, entities, 0));
        return entities;
    }

    /// <summary>
    /// Batch counterpart of <see cref="CreateEntity(EntityTemplate)"/>: reserves
    /// <paramref name="count"/> real <see cref="Entity"/> ids immediately and queues their
    /// placement, all sharing <paramref name="template"/>'s components/tags, blitted in one
    /// <see cref="Internal.ComponentStorage{T}.Fill"/> call per component regardless of
    /// <paramref name="count"/>. Throws <see cref="InvalidOperationException"/> if
    /// <paramref name="template"/> has children (<see cref="EntityTemplate.Children"/>) —
    /// each child is a distinct set of entities per instance, so there's no blitting trick
    /// that applies to a tree; call <see cref="CreateEntity(EntityTemplate)"/> once per
    /// instance instead. Returns <see cref="Array.Empty{T}"/> for <paramref name="count"/> == 0;
    /// throws <see cref="ArgumentOutOfRangeException"/> for a negative count.
    /// </summary>
    public Entity[] CreateEntity(EntityTemplate template, int count)
    {
        if (template.Children.Count > 0)
            throw new InvalidOperationException("Batch instantiation is not supported for a template with children — each child is a distinct set of entities per instance. Call CreateEntity(EntityTemplate) once per instance instead.");

        if (count == 0) return Array.Empty<Entity>();
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), count, "count must be non-negative.");

        var entities = new Entity[count];
        _world.ReserveEntityRange(entities);

        lock (_gate) Enqueue(new QueuedCommand(default, BatchTemplatePlacementOp.Apply, (entities, template), 0));
        return entities;
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
    /// command runs (an earlier queued <see cref="World.AddComponent{T}(Entity)"/> for the same entity,
    /// or one from a previous batch that was never removed), this overwrites it instead
    /// of adding a second one. Last-queued value wins, the same
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
    /// Queues a <typeparamref name="T"/> edge from <paramref name="source"/> to
    /// <paramref name="target"/> carrying <paramref name="value"/>. A no-op at apply time
    /// if either entity is dead. If this edge already exists, its value is overwritten;
    /// last-queued value wins, same as <see cref="AddComponent{T}(Entity, T)"/>. Adding a
    /// relation type's first edge (or removing its last, via <see cref="RemoveRelation{T}"/>)
    /// moves the owning entity to a different archetype; every edge in between is an O(1)
    /// dictionary write, no archetype move. If <typeparamref name="T"/> implements
    /// <see cref="IExclusiveRelation"/>, any other existing target is replaced rather than
    /// added alongside; see that interface's own doc. Safe to call concurrently from
    /// several threads at once.
    /// </summary>
    public void AddRelation<T>(Entity source, Entity target, T value) where T : struct, IRelation
    {
        lock (_gate)
        {
            var buffer = GetAddRelationBuffer<T>();
            Internal.ArrayGrowth.EnsureCapacity(ref buffer.Items, buffer.Count + 1);
            var slot = buffer.Count++;
            buffer.Items[slot] = (target, value);
            Enqueue(new QueuedCommand(source, AddRelationOp<T>.Apply, buffer, slot));
        }
    }

    /// <summary>Same as <see cref="AddRelation{T}(Entity, Entity, T)"/>, with the edge's payload defaulted. Convenience for a marker-only relation type (no fields), so a caller doesn't have to spell out <c>default(T)</c> explicitly.</summary>
    public void AddRelation<T>(Entity source, Entity target) where T : struct, IRelation => AddRelation(source, target, default(T));

    /// <summary>Queues removing the <typeparamref name="T"/> edge from <paramref name="source"/> to <paramref name="target"/>, if it exists. A no-op at apply time otherwise. Safe to call concurrently from several threads at once.</summary>
    public void RemoveRelation<T>(Entity source, Entity target) where T : struct, IRelation
    {
        lock (_gate)
        {
            var buffer = GetRelationTargetBuffer();
            Internal.ArrayGrowth.EnsureCapacity(ref buffer.Items, buffer.Count + 1);
            var slot = buffer.Count++;
            buffer.Items[slot] = target;
            Enqueue(new QueuedCommand(source, RemoveRelationOp<T>.Apply, buffer, slot));
        }
    }

    /// <summary>
    /// Applies every queued command, in queued order, then clears the queue. Each command
    /// re-checks <see cref="World.IsAlive"/> at its own point in the sequence, so an earlier
    /// command that destroys an entity silently invalidates any later command in the same
    /// batch still targeting it rather than throwing. What can still throw is consumer code
    /// reached through a structural-change notification. Cleanup (clearing the queue,
    /// resetting the per-type buffers) runs in a <c>finally</c> regardless, so a misbehaving
    /// observer never leaves the batch half-applied for the next <see cref="Apply"/> call to
    /// silently replay. Only ever called single-threaded, from a stage's join point after
    /// every enqueueing thread has already returned, so neither the replay loop nor the
    /// cleanup needs <see cref="_gate"/>.
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
            Array.Clear(_queue, 0, _count);
            _count = 0;

            foreach (var buffer in _addComponentBuffers)
                (buffer as IResettableBuffer)?.ResetForNextBatch();
            foreach (var buffer in _addRelationBuffers)
                (buffer as IResettableBuffer)?.ResetForNextBatch();
            _relationTargetBuffer?.ResetForNextBatch();
        }
    }
}
