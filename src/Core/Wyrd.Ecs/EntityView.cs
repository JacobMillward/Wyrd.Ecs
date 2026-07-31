namespace Wyrd.Ecs;

/// <summary>
/// The idiomatic single-entity handle: once you already have a stored <see cref="Entity"/>
/// and want to read or change one or more things about it, this is the front door — same
/// vocabulary as <see cref="World"/>/<see cref="CommandBuffer"/>, just without repeating
/// the entity at every call. Reads (<c>Get</c>/<c>TryGet</c>/<c>Has</c>) are immediate,
/// forwarding straight to the matching <see cref="World"/> method — the same tracked
/// path, no new tracking logic. Mutations (<c>Add</c>/<c>Remove</c>/<c>Destroy</c>)
/// forward to whichever <see cref="CommandBuffer"/> this view was constructed with (the
/// world's default <see cref="World.Commands"/> via <see cref="World.this[Entity]"/>, or
/// a specific buffer via <see cref="CommandBuffer.CreateEntity()"/>) — never hardcoded to
/// the default buffer, so a view built from a <see cref="World.CreateCommands"/> buffer
/// keeps its mutations on that same buffer. Mutation methods return this view so calls
/// can chain: <c>world[e].AddComponent(pos).AddComponent(vel).AddTag&lt;Enemy&gt;()</c>.
/// Direct <see cref="World"/>/<see cref="CommandBuffer"/> calls remain the right tool for
/// iterating many different entities without holding a view per-entity, and for hot-path
/// code that already has a <see cref="ChunkAction{TAccess0}"/> accessor. A
/// <c>ref struct</c> so it can never be smuggled into a field or held past the current
/// scope. Not the tool for a hot per-entity loop — every accessor goes through
/// <see cref="Entity"/>'s location-resolution indirection on every call (necessary to
/// remain correct after a structural move), unlike <see cref="ArchetypeChunk.Access{TAccessor}"/>'s
/// pre-cached, per-chunk span access. See the design's Entity identity section.
/// </summary>
public readonly ref struct EntityView
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

    /// <summary>Returns a tracked mutable reference to this entity's <typeparamref name="T"/>. Throws if the entity does not have the component.</summary>
    public ref T GetComponent<T>() where T : struct, IComponent => ref _world.GetComponent<T>(_entity);

    /// <summary>Same as <see cref="World.TryGetComponent{T}(Entity, out bool)"/>, without repeating the entity.</summary>
    public ref T TryGetComponent<T>(out bool found) where T : struct, IComponent => ref _world.TryGetComponent<T>(_entity, out found);

    /// <summary>True if this entity has a <typeparamref name="T"/> component.</summary>
    public bool HasComponent<T>() where T : struct, IComponent => _world.HasComponent<T>(_entity);

    /// <summary>Queues adding <paramref name="value"/> to this entity — see <see cref="CommandBuffer.AddComponent{T}(Entity, T)"/>.</summary>
    public EntityView AddComponent<T>(T value) where T : struct, IComponent
    {
        _commands.AddComponent(_entity, value);
        return this;
    }

    /// <summary>Queues removing <typeparamref name="T"/> from this entity — see <see cref="CommandBuffer.RemoveComponent{T}(Entity)"/>.</summary>
    public EntityView RemoveComponent<T>() where T : struct, IComponent
    {
        _commands.RemoveComponent<T>(_entity);
        return this;
    }

    /// <summary>True if this entity has tag <typeparamref name="T"/>.</summary>
    public bool HasTag<T>() where T : struct, ITag => _world.HasTag<T>(_entity);

    /// <summary>Queues adding tag <typeparamref name="T"/> to this entity — see <see cref="CommandBuffer.AddTag{T}(Entity)"/>.</summary>
    public EntityView AddTag<T>() where T : struct, ITag
    {
        _commands.AddTag<T>(_entity);
        return this;
    }

    /// <summary>Queues removing tag <typeparamref name="T"/> from this entity — see <see cref="CommandBuffer.RemoveTag{T}(Entity)"/>.</summary>
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

    /// <summary>Queues a <typeparamref name="T"/> edge from this entity to <paramref name="target"/> carrying <paramref name="value"/> — see <see cref="CommandBuffer.AddRelation{T}(Entity, Entity, T)"/>.</summary>
    public EntityView AddRelation<T>(Entity target, T value) where T : struct, IRelation
    {
        _commands.AddRelation(_entity, target, value);
        return this;
    }

    /// <summary>Same as <see cref="AddRelation{T}(Entity, T)"/>, with the edge's payload defaulted — see <see cref="CommandBuffer.AddRelation{T}(Entity, Entity)"/>.</summary>
    public EntityView AddRelation<T>(Entity target) where T : struct, IRelation
    {
        _commands.AddRelation<T>(_entity, target);
        return this;
    }

    /// <summary>Queues removing the <typeparamref name="T"/> edge from this entity to <paramref name="target"/>, if it exists — see <see cref="CommandBuffer.RemoveRelation{T}(Entity, Entity)"/>.</summary>
    public EntityView RemoveRelation<T>(Entity target) where T : struct, IRelation
    {
        _commands.RemoveRelation<T>(_entity, target);
        return this;
    }

    /// <summary>Every target this entity has a <typeparamref name="T"/> edge to, and each edge's payload — see <see cref="World.Targets{T}(Entity)"/>.</summary>
    public IReadOnlyDictionary<Entity, T> Targets<T>() where T : struct, IRelation => _world.Targets<T>(_entity);

    /// <summary>Every source entity with a <typeparamref name="T"/> edge pointing at this entity — see <see cref="World.Sources{T}(Entity)"/>.</summary>
    public IReadOnlyCollection<Entity> Sources<T>() where T : struct, IRelation => _world.Sources<T>(_entity);

    /// <summary>True if this entity is still alive.</summary>
    public bool IsAlive => _world.IsAlive(_entity);

    /// <summary>This entity's permanent, opaque identity — see <see cref="EntityId"/>.</summary>
    public EntityId PermanentId => _world.GetPermanentId(_entity);

    /// <summary>Queues destroying this entity — see <see cref="CommandBuffer.DestroyEntity(Entity)"/>.</summary>
    public void DestroyEntity() => _commands.DestroyEntity(_entity);
}
