namespace Wyrd.Ecs;

/// <summary>
/// Configures and constructs a <see cref="World"/>: archetype capacity, the parallel
/// dispatch threshold, and the systems it runs.
/// </summary>
public sealed class WorldBuilder
{
    private int _archetypeCapacity = World.DefaultArchetypeCapacity;
    private readonly List<SystemEntry> _pending = [];
    private int _parallelThreshold = 1000;
    private ISystemScheduler? _scheduler;

    /// <summary>
    /// Sets the entity capacity every archetype's dense arrays start at and never shrink
    /// below. Too low means repeated doubling growth on any archetype with more than a
    /// handful of entities; too high wastes memory per archetype, multiplied by however
    /// many distinct archetypes the game creates.
    /// </summary>
    public WorldBuilder WithArchetypeCapacity(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Archetype capacity must be positive.");

        _archetypeCapacity = capacity;
        return this;
    }

    /// <summary>
    /// Raised once, immediately after <see cref="Build"/> constructs the
    /// <see cref="World"/>: the extensibility hook a package (such as
    /// Wyrd.Ecs.Persistence) uses to associate its own configuration with the
    /// resulting World.
    /// </summary>
    public event Action<World>? OnBuilt;

    /// <summary>
    /// Builds a new <see cref="World"/> with the configured options, including whatever
    /// <c>AddSystem&lt;T&gt;()</c> registered. The returned <see cref="World"/> already
    /// owns a static parallel schedule (empty if no systems were registered) and drives
    /// it itself via <see cref="World.Update"/>.
    /// </summary>
    public World Build()
    {
        var scheduler = _scheduler ?? new ParallelSystemScheduler(_parallelThreshold);
        var world = new World(_archetypeCapacity, scheduler);
        scheduler.InitialRegister(_pending, world);

        OnBuilt?.Invoke(world);
        return world;
    }

    /// <summary>
    /// Registers one system, deferring its construction until <see cref="Build"/> (so a
    /// <c>ctor(World)</c> can receive the <see cref="World"/> being built). Not called
    /// directly by consumer code — the generator emits a strongly-typed
    /// <c>AddSystem&lt;T&gt;()</c> overload closing over this, resolving
    /// <paramref name="access"/>/<paramref name="construct"/>/<paramref name="generatedBeforeTargets"/>/
    /// <paramref name="generatedAfterTargets"/> from <c>Wyrd.Ecs.Generated.SystemRegistry</c>
    /// so none of them are ever spelled out by hand. <paramref name="generatedBeforeTargets"/>/
    /// <paramref name="generatedAfterTargets"/> seed the entry's edges (from
    /// <c>[RunBefore]</c>/<c>[RunAfter]</c>) before any <see cref="SystemRegistration.Before{T}"/>/
    /// <see cref="SystemRegistration.After{T}"/> chained afterward adds more — both sets
    /// union into the same list. This has to happen inside the core assembly (here),
    /// not in the generated extension method that calls it: <see cref="SystemRegistration"/>
    /// exposes its underlying <see cref="SystemEntry"/> only as `internal` (test-only
    /// introspection), unreachable from a consumer assembly generated code lives in
    /// (which gets no <c>InternalsVisibleTo</c> grant), so seeding can't happen by
    /// reaching into <see cref="SystemRegistration"/> from outside — it has to happen
    /// here, where the entry is directly available. Returns a chainable
    /// <see cref="SystemRegistration"/> for declaring further ordering edges or starting
    /// the system disabled.
    /// </summary>
    public SystemRegistration AddSystemCore(
        Type systemType,
        SystemAccess? access,
        Func<World, EcsSystem> construct,
        IReadOnlyList<Type> generatedBeforeTargets,
        IReadOnlyList<Type> generatedAfterTargets)
    {
        var entry = RegisterEntry(systemType, access, construct, generatedBeforeTargets, generatedAfterTargets);
        return new SystemRegistration(RegisterEntry, Build, entry);
    }

    private SystemEntry RegisterEntry(
        Type systemType,
        SystemAccess? access,
        Func<World, EcsSystem> construct,
        IReadOnlyList<Type> generatedBeforeTargets,
        IReadOnlyList<Type> generatedAfterTargets)
    {
        var entry = new SystemEntry { SystemType = systemType, Construct = construct, Access = access };
        entry.BeforeTargets.AddRange(generatedBeforeTargets);
        entry.AfterTargets.AddRange(generatedAfterTargets);
        _pending.Add(entry);
        return entry;
    }

    /// <summary>
    /// Sets the minimum <see cref="World.TotalEntityCount"/> a stage needs before
    /// <see cref="ParallelSystemScheduler"/> dispatches it to the thread pool instead of
    /// running it inline, since dispatch overhead can outweigh the parallelism gain at
    /// small world sizes. Defaults to 1000, a starting point, not a benchmarked value.
    /// No effect if <see cref="WithScheduler"/> supplied a different <see cref="ISystemScheduler"/>.
    /// </summary>
    public WorldBuilder WithParallelThreshold(int entityCount)
    {
        _parallelThreshold = entityCount;
        return this;
    }

    /// <summary>
    /// Overrides the default <see cref="ParallelSystemScheduler"/> — the extensibility
    /// hook for a custom <see cref="ISystemScheduler"/> (e.g. a deterministic,
    /// non-parallel implementation for lockstep/replay netcode).
    /// </summary>
    public WorldBuilder WithScheduler(ISystemScheduler scheduler)
    {
        _scheduler = scheduler;
        return this;
    }
}
