namespace Wyrd.Ecs;

/// <summary>
/// Constructs a <see cref="World"/>. Exists as the entry point future construction-time
/// configuration (such as registering Systems) will extend, alongside the options
/// already here.
/// </summary>
public sealed class WorldBuilder
{
    private int _archetypeCapacity = World.DefaultArchetypeCapacity;
    private IReadOnlyDictionary<Type, SystemAccess>? _generatedAccess;
    private OrderedSystem[] _systems = [];
    private int _parallelThreshold = 1000;

    /// <summary>
    /// Sets the entity capacity every archetype's dense arrays (its entity list and
    /// each component column) start at and never shrink below. The right value is
    /// specific to a game's actual entity/archetype-count distribution: too low means
    /// paying for repeated doubling growth on every archetype that ends up with more
    /// than a handful of entities; too high wastes memory on every archetype that
    /// never gets close to it, multiplied by however many distinct archetypes the game
    /// creates.
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
    /// <see cref="World"/> — the extensibility hook a package (such as
    /// Wyrd.Ecs.Persistence) uses to associate configuration made on this builder
    /// with the resulting World, since neither WorldBuilder nor World can gain new
    /// fields from another assembly.
    /// </summary>
    public event Action<World>? OnBuilt;

    /// <summary>
    /// Builds a new <see cref="World"/> with the configured options, including whatever
    /// <see cref="WithSystems"/> registered — the returned <see cref="World"/> already owns
    /// a static parallel schedule (empty if <see cref="WithSystems"/> was never called) and
    /// drives it itself via <see cref="World.Tick"/>.
    /// </summary>
    public World Build()
    {
        var stages = Internal.SystemScheduler.BuildStages(_systems, _generatedAccess ?? new Dictionary<Type, SystemAccess>());
        var world = new World(_archetypeCapacity, new ScheduledExecutor(stages, _parallelThreshold));
        OnBuilt?.Invoke(world);
        return world;
    }

    /// <summary>
    /// Registers the systems <see cref="Build"/> will schedule, along with
    /// the generated <c>Type → SystemAccess</c> registry the query-chain generator
    /// emits into the calling project (<c>Wyrd.Ecs.Generated.GeneratedSystemAccess.Entries</c>) —
    /// passed explicitly by the caller, since <see cref="WorldBuilder"/> lives in
    /// <c>Wyrd.Ecs</c> itself and can't reference a type generated into a consumer's
    /// own compilation. Each <paramref name="systems"/> element converts implicitly from
    /// a bare <see cref="EcsSystem"/> (see <see cref="OrderedSystem"/>) when it declares
    /// no Before/After edges, or from <see cref="Order.For"/> when it does. Registration
    /// order is the tiebreak among systems with no ordering relationship to each other;
    /// declared edges take precedence over it.
    /// </summary>
    public WorldBuilder WithSystems(IReadOnlyDictionary<Type, SystemAccess> generatedAccess, params OrderedSystem[] systems)
    {
        _generatedAccess = generatedAccess;
        _systems = systems;
        return this;
    }

    /// <summary>
    /// Sets the minimum <see cref="World.TotalEntityCount"/> a stage needs before
    /// <see cref="ScheduledExecutor"/> dispatches it to the thread pool instead of
    /// running it inline — a stage below this threshold runs sequentially even if it
    /// has more than one system, since thread-pool dispatch overhead can outweigh the
    /// parallelism gain at small world sizes. Defaults to 1000, a starting point, not
    /// a benchmarked value.
    /// </summary>
    public WorldBuilder WithParallelThreshold(int entityCount)
    {
        _parallelThreshold = entityCount;
        return this;
    }
}
