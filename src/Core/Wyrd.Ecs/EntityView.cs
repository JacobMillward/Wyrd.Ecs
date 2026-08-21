namespace Wyrd.Ecs;

/// <summary>
/// The idiomatic single-entity handle: once you already have a stored <see cref="Entity"/>
/// and want to read or change one or more things about it, this is the front door. Reads
/// (<c>Get</c>/<c>TryGet</c>/<c>Has</c>) forward straight to the matching
/// <see cref="World"/> method. Mutations (<c>Add</c>/<c>Remove</c>/<c>Destroy</c>) queue on
/// whichever <see cref="CommandBuffer"/> this view was constructed with (the world's
/// default <see cref="World.Commands"/> via <see cref="World.this[Entity]"/>, or a specific
/// buffer via <see cref="CommandBuffer.CreateEntity()"/>). Mutation methods return this
/// view so calls can chain:
/// <c>world[e].AddComponent(pos).AddComponent(vel).AddTag&lt;Enemy&gt;()</c>. A
/// <c>ref struct</c>, so it can never be stored in a field or held past the current scope.
/// Not the right tool for a hot per-entity loop: use <see cref="World"/>/
/// <see cref="CommandBuffer"/> directly, or a <see cref="ChunkAction{TAccess0}"/> accessor.
/// </summary>
public readonly ref struct EntityView : IComponentSink
{
    private readonly World _world;
    private readonly CommandBuffer _commands;
    private readonly Entity _entity;

    internal EntityView(World world, CommandBuffer commands, Entity entity)
    {
        _world = world;
        _commands = commands;
        _entity = entity;
    }

    /// <summary>The <see cref="Wyrd.Ecs.Entity"/> this view is bound to.</summary>
    public Entity Entity => _entity;

    /// <summary>Unwraps to the bound <see cref="Wyrd.Ecs.Entity"/>, same value as <see cref="Entity"/>. Lets a storable id fall out of an assignment like <c>Entity e = commands.CreateEntity();</c> without naming the property.</summary>
    public static implicit operator Entity(EntityView view) => view._entity;

    /// <summary>Returns a tracked mutable reference to this entity's <typeparamref name="T"/>. Throws if the entity does not have the component.</summary>
    public ref T GetComponent<T>() where T : struct, IComponent => ref _world.GetComponent<T>(_entity);

    /// <summary>Same as <see cref="World.TryGetComponent{T}(Entity, out bool)"/>, without repeating the entity.</summary>
    public ref T TryGetComponent<T>(out bool found) where T : struct, IComponent => ref _world.TryGetComponent<T>(_entity, out found);

    /// <summary>True if this entity has a <typeparamref name="T"/> component.</summary>
    public bool HasComponent<T>() where T : struct, IComponent => _world.HasComponent<T>(_entity);

    /// <summary>Queues adding <paramref name="value"/> to this entity. See <see cref="CommandBuffer.AddComponent{T}(Entity, T)"/>.</summary>
    public EntityView AddComponent<T>(T value) where T : struct, IComponent
    {
        _commands.AddComponent(_entity, value);
        return this;
    }

    /// <summary>
    /// Queues <see cref="Transform"/>, and, unless <paramref name="isStatic"/>, a matching
    /// <see cref="PreviousTransform"/> (equal to <paramref name="value"/>) too. Adding only
    /// <see cref="Transform"/> for an entity meant to move would leave it unmatched by
    /// <see cref="TransformSnapshotSystem"/>'s query (it requires both) and would make the
    /// first interpolated read snap from a stale/default <see cref="PreviousTransform"/>
    /// instead of holding steady, so <paramref name="isStatic"/> defaults to <c>false</c>.
    /// </summary>
    public EntityView AddTransform(Transform value, bool isStatic = false)
    {
        AddComponent(value);
        if (!isStatic)
            AddComponent(new PreviousTransform { Position = value.Position, Rotation = value.Rotation, Scale = value.Scale });
        return this;
    }

    /// <inheritdoc/>
    void IComponentSink.AddComponent<T>(T value) => AddComponent(value);

    /// <summary>Adds every component in <paramref name="bundle"/> to this entity. See <see cref="IComponentBundle"/>.</summary>
    public EntityView Add<TBundle>(TBundle bundle) where TBundle : IComponentBundle
    {
        bundle.ApplyTo(this);
        return this;
    }

    /// <summary>Queues removing <typeparamref name="T"/> from this entity. See <see cref="CommandBuffer.RemoveComponent{T}(Entity)"/>.</summary>
    public EntityView RemoveComponent<T>() where T : struct, IComponent
    {
        _commands.RemoveComponent<T>(_entity);
        return this;
    }

    /// <summary>True if this entity has tag <typeparamref name="T"/>.</summary>
    public bool HasTag<T>() where T : struct, ITag => _world.HasTag<T>(_entity);

    /// <summary>Queues adding tag <typeparamref name="T"/> to this entity. See <see cref="CommandBuffer.AddTag{T}(Entity)"/>.</summary>
    public EntityView AddTag<T>() where T : struct, ITag
    {
        _commands.AddTag<T>(_entity);
        return this;
    }

    /// <summary>Queues removing tag <typeparamref name="T"/> from this entity. See <see cref="CommandBuffer.RemoveTag{T}(Entity)"/>.</summary>
    public EntityView RemoveTag<T>() where T : struct, ITag
    {
        _commands.RemoveTag<T>(_entity);
        return this;
    }

    /// <summary>True if this entity has a <typeparamref name="T"/> edge to <paramref name="target"/>.</summary>
    public bool HasRelation<T>(Entity target) where T : struct, IRelation => _world.HasRelation<T>(_entity, target);

    /// <summary>Same as <see cref="World.GetRelation{T}(Entity, Entity)"/>, without repeating the entity.</summary>
    public ref T GetRelation<T>(Entity target) where T : struct, IRelation => ref _world.GetRelation<T>(_entity, target);

    /// <summary>Same as <see cref="World.TryGetRelation{T}(Entity, Entity, out bool)"/>, without repeating the entity.</summary>
    public ref T TryGetRelation<T>(Entity target, out bool found) where T : struct, IRelation => ref _world.TryGetRelation<T>(_entity, target, out found);

    /// <summary>Queues a <typeparamref name="T"/> edge from this entity to <paramref name="target"/> carrying <paramref name="value"/>. See <see cref="CommandBuffer.AddRelation{T}(Entity, Entity, T)"/>.</summary>
    public EntityView AddRelation<T>(Entity target, T value) where T : struct, IRelation
    {
        _commands.AddRelation(_entity, target, value);
        return this;
    }

    /// <summary>Same as <see cref="AddRelation{T}(Entity, T)"/>, with the edge's payload defaulted. See <see cref="CommandBuffer.AddRelation{T}(Entity, Entity)"/>.</summary>
    public EntityView AddRelation<T>(Entity target) where T : struct, IRelation
    {
        _commands.AddRelation<T>(_entity, target);
        return this;
    }

    /// <summary>Queues removing the <typeparamref name="T"/> edge from this entity to <paramref name="target"/>, if it exists. See <see cref="CommandBuffer.RemoveRelation{T}(Entity, Entity)"/>.</summary>
    public EntityView RemoveRelation<T>(Entity target) where T : struct, IRelation
    {
        _commands.RemoveRelation<T>(_entity, target);
        return this;
    }

    /// <summary>Queues this entity's <see cref="Parent"/> edge to <paramref name="parent"/>. Replaces any existing parent in place, since <see cref="Parent"/> is exclusive. To reparent, call this alone; a preceding <see cref="ClearParent"/> costs an extra, avoidable archetype move.</summary>
    public EntityView SetParent(Entity parent)
    {
        _commands.AddRelation<Parent>(_entity, parent);
        return this;
    }

    /// <summary>Queues removing this entity's current <see cref="Parent"/> edge, if it has one. A no-op if it doesn't. To reparent, use <see cref="SetParent"/> alone rather than this followed by it.</summary>
    public EntityView ClearParent()
    {
        if (_world.TryGetParent(_entity, out var parent))
            _commands.RemoveRelation<Parent>(_entity, parent);
        return this;
    }

    /// <summary>Queues a <see cref="Parent"/> edge from <paramref name="child"/> to this entity, same edge as <c>child.SetParent(this)</c>, called from the parent's side. A parent may have any number of children, so unlike <see cref="SetParent"/> this never replaces an existing edge other than <paramref name="child"/>'s own.</summary>
    public EntityView AddChild(Entity child)
    {
        _commands.AddRelation<Parent>(child, _entity);
        return this;
    }

    /// <summary>Queues removing <paramref name="child"/>'s <see cref="Parent"/> edge to this entity, if it exists. Same edge as <c>child.ClearParent()</c>, called from the parent's side.</summary>
    public EntityView RemoveChild(Entity child)
    {
        _commands.RemoveRelation<Parent>(child, _entity);
        return this;
    }

    /// <summary>Same as <see cref="World.TryGetParent(Entity, out Entity)"/>, without repeating the entity.</summary>
    public bool TryGetParent(out Entity parent) => _world.TryGetParent(_entity, out parent);

    /// <summary>Same as <see cref="World.GetParent(Entity)"/>, without repeating the entity.</summary>
    public Entity GetParent() => _world.GetParent(_entity);

    /// <summary>Every direct child of this entity. See <see cref="World.Children(Entity)"/>.</summary>
    public IReadOnlyCollection<Entity> Children() => _world.Children(_entity);

    /// <summary>This entity's parent chain, closest parent first. See <see cref="World.Ancestors(Entity)"/>.</summary>
    public IEnumerable<Entity> Ancestors() => _world.Ancestors(_entity);

    /// <summary>Every descendant of this entity, depth-first. See <see cref="World.Descendants(Entity)"/>.</summary>
    public IEnumerable<Entity> Descendants() => _world.Descendants(_entity);

    /// <summary>Every target this entity has a <typeparamref name="T"/> edge to, and each edge's payload. See <see cref="World.Targets{T}(Entity)"/>.</summary>
    public IReadOnlyDictionary<Entity, T> Targets<T>() where T : struct, IRelation => _world.Targets<T>(_entity);

    /// <summary>Every source entity with a <typeparamref name="T"/> edge pointing at this entity. See <see cref="World.Sources{T}(Entity)"/>.</summary>
    public IReadOnlyCollection<Entity> Sources<T>() where T : struct, IRelation => _world.Sources<T>(_entity);

    /// <summary>True if this entity is still alive.</summary>
    public bool IsAlive => _world.IsAlive(_entity);

    /// <summary>This entity's permanent, opaque identity. See <see cref="EntityId"/>.</summary>
    public EntityId PermanentId => _world.GetPermanentId(_entity);

    /// <summary>Queues destroying this entity. See <see cref="CommandBuffer.DestroyEntity(Entity)"/>.</summary>
    public void DestroyEntity() => _commands.DestroyEntity(_entity);
}
