namespace Wyrd.Ecs;

/// <summary>
/// Configures and constructs a <see cref="World"/>: archetype capacity, the parallel
/// dispatch threshold, and the systems it runs.
/// </summary>
public sealed class WorldBuilder
{
    private int _archetypeCapacity = World.DefaultArchetypeCapacity;
    private IReadOnlyDictionary<Type, SystemAccess>? _generatedAccess;
    private OrderedSystem[] _systems = [];
    private int _parallelThreshold = 1000;

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
    /// <see cref="WithSystems(IReadOnlyDictionary{Type, SystemAccess}, OrderedSystem[])"/>
    /// registered. The returned <see cref="World"/> already owns a static parallel
    /// schedule (empty if no systems were registered) and drives it itself via
    /// <see cref="World.Update"/>.
    /// </summary>
    public World Build()
    {
        var stages = Internal.SystemScheduler.BuildStages(_systems, _generatedAccess ?? new Dictionary<Type, SystemAccess>());
        var world = new World(_archetypeCapacity, new ScheduledExecutor(stages, _parallelThreshold));
        OnBuilt?.Invoke(world);
        return world;
    }

    /// <summary>
    /// Registers the systems <see cref="Build"/> will schedule, along with the generated
    /// <c>Type -&gt; SystemAccess</c> registry the query-chain generator emits into the
    /// calling project, passed explicitly since <see cref="WorldBuilder"/> can't
    /// reference a type generated into a consumer's own compilation. Each
    /// <paramref name="systems"/> element converts implicitly from a bare
    /// <see cref="EcsSystem"/>, or from <see cref="Order.For"/> when it declares
    /// Before/After edges. Registration order is the tiebreak among systems with no
    /// ordering relationship; declared edges take precedence over it.
    /// </summary>
    public WorldBuilder WithSystems(IReadOnlyDictionary<Type, SystemAccess> generatedAccess, params OrderedSystem[] systems)
    {
        _generatedAccess = generatedAccess;
        _systems = systems;
        return this;
    }

    /// <summary>
    /// Same as the <c>params OrderedSystem[]</c> overload above, for a caller that
    /// already has an assembled <see cref="EcsSystem"/> collection: the implicit
    /// <see cref="OrderedSystem"/> conversion applies per-argument, not across an
    /// array's element type, so a plain collection needs this explicit overload.
    /// </summary>
    public WorldBuilder WithSystems(IReadOnlyDictionary<Type, SystemAccess> generatedAccess, IReadOnlyList<EcsSystem> systems)
    {
        _generatedAccess = generatedAccess;
        _systems = [.. systems.Select(s => (OrderedSystem)s)];
        return this;
    }

    /// <summary>
    /// Sets the minimum <see cref="World.TotalEntityCount"/> a stage needs before
    /// <see cref="ScheduledExecutor"/> dispatches it to the thread pool instead of
    /// running it inline, since dispatch overhead can outweigh the parallelism gain at
    /// small world sizes. Defaults to 1000, a starting point, not a benchmarked value.
    /// </summary>
    public WorldBuilder WithParallelThreshold(int entityCount)
    {
        _parallelThreshold = entityCount;
        return this;
    }
}
