namespace Wyrd.Ecs;

/// <summary>
/// A non-storable, <see cref="World"/>-scoped bound view over one entity: the "I have
/// a stored <see cref="Entity"/>, fetch a component now" convenience, for use outside
/// any active query iteration. Backed by <see cref="World.GetComponent{T}(Entity)"/> — the
/// same tracked path, no new tracking logic, just a call-site convenience. A
/// <c>ref struct</c> so it can never be smuggled into a field or held past the
/// current scope. Not the tool for a hot per-entity loop — it goes through
/// <see cref="Entity"/>'s location-resolution indirection on every call (necessary to
/// remain correct after a structural move), unlike <see cref="ArchetypeChunk.Access{TAccessor}"/>'s
/// pre-cached, per-chunk span access. See the design's Entity identity
/// section.
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

    /// <summary>Returns a tracked mutable reference to this entity's <typeparamref name="T"/>.</summary>
    public ref T GetComponent<T>() where T : struct, IComponent => ref _world.GetComponent<T>(_entity);
}
