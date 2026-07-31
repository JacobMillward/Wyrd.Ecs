namespace Wyrd.Ecs;

/// <summary>
/// The idiomatic single-entity handle: once you already have a stored <see cref="Entity"/>
/// and want to read or change one or more things about it, this is the front door — same
/// vocabulary as <see cref="World"/>/<see cref="CommandBuffer"/>, just without repeating
/// the entity at every call. Reads (<c>Get</c>/<c>TryGet</c>/<c>Has</c>) are immediate,
/// forwarding straight to the matching <see cref="World"/> method — the same tracked
/// path, no new tracking logic. Mutations (<c>Add</c>/<c>Remove</c>/<c>Destroy</c>)
/// forward to <see cref="World.Commands"/> and so share its deferred,
/// apply-on-<see cref="World.ApplyCommands()"/> semantics — nothing here is a new
/// mutation path. Mutation methods return this view so calls can chain:
/// <c>world[e].AddComponent(pos).AddComponent(vel).AddTag&lt;Enemy&gt;()</c>. Direct
/// <see cref="World"/>/<see cref="CommandBuffer"/> calls remain the right tool for
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
    private readonly Entity _entity;

    internal EntityView(World world, Entity entity)
    {
        _world = world;
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
        _world.Commands.AddComponent(_entity, value);
        return this;
    }

    /// <summary>Queues removing <typeparamref name="T"/> from this entity — see <see cref="CommandBuffer.RemoveComponent{T}(Entity)"/>.</summary>
    public EntityView RemoveComponent<T>() where T : struct, IComponent
    {
        _world.Commands.RemoveComponent<T>(_entity);
        return this;
    }
}
