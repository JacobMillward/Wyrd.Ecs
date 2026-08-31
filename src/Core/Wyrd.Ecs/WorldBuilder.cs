using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs;

/// <summary>
/// Configures and constructs a <see cref="World"/>: archetype capacity, the parallel
/// dispatch threshold, and the systems it runs. Single-use: call <see cref="Build"/>
/// exactly once. Building twice (or configuring further afterward) throws - see
/// <see cref="ThrowIfAlreadyBuilt"/>'s doc comment for why silently allowing it would be
/// worse than rejecting it.
/// </summary>
public sealed class WorldBuilder
{
    private int _archetypeCapacity = World.DefaultArchetypeCapacity;
    private readonly List<SystemEntry> _pending = [];
    private readonly List<(Type ResourceType, Action<World> Apply)> _pendingResources = [];
    private int _parallelThreshold = 1000;
    private ISystemScheduler? _scheduler;
    private bool _built;
    private TimeSpan _fixedStep = TimeSpan.FromSeconds(1.0 / 60.0);
    private int _maxSubstepsPerUpdate = 5;
    private TimeSpan _maxDelta = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Sets the entity capacity every archetype's dense arrays start at and never shrink
    /// below. Too low means repeated doubling growth on any archetype with more than a
    /// handful of entities; too high wastes memory per archetype, multiplied by however
    /// many distinct archetypes the game creates.
    /// </summary>
    public WorldBuilder WithArchetypeCapacity(int capacity)
    {
        ThrowIfAlreadyBuilt();
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
    /// it itself via <see cref="World.Update"/>. Throws if called more than once on the
    /// same builder - see <see cref="ThrowIfAlreadyBuilt"/>.
    /// </summary>
    public World Build()
    {
        ThrowIfAlreadyBuilt();
        _built = true;

        var sortedPending = SortByConstructionDependency(_pending);

        var scheduler = _scheduler ?? new ParallelSystemScheduler(_parallelThreshold);
        var world = new World(_archetypeCapacity, scheduler, _fixedStep, _maxSubstepsPerUpdate, _maxDelta);
        foreach (var (_, apply) in _pendingResources) apply(world);
        scheduler.InitialRegister(sortedPending, world);

        OnBuilt?.Invoke(world);
        return world;
    }

    /// <summary>
    /// Orders <paramref name="pending"/> so every entry's
    /// <see cref="SystemEntry.ConstructionDependencies"/> is constructed before it,
    /// independent of the order <c>AddSystemCore</c> was called in - e.g. <c>.AddRenderer()</c>
    /// before <c>.AddWindow()</c> in the same chain builds correctly, exactly as if they'd
    /// been called in the other order. Throws <see cref="InvalidOperationException"/> if a
    /// declared dependency was never registered on this builder, or if dependencies form a
    /// cycle (unreachable through any dependency this repo's own extension methods declare
    /// today, but the check is generic, same as <see cref="Internal.StableTopologicalSort"/>
    /// itself).
    /// </summary>
    private static List<SystemEntry> SortByConstructionDependency(List<SystemEntry> pending)
    {
        var byType = pending.ToDictionary(e => e.SystemType);
        var nodes = pending.Select(e => e.SystemType).ToList();
        var edges = new List<StableTopologicalSort.Edge<Type>>();
        foreach (var entry in pending)
        {
            foreach (var dependency in entry.ConstructionDependencies)
            {
                if (!byType.ContainsKey(dependency))
                    throw new InvalidOperationException(
                        $"A system of type '{entry.SystemType}' declares a construction dependency on " +
                        $"'{dependency}', but no system of that type is registered on this WorldBuilder. " +
                        "Register it before calling Build().");

                edges.Add(new StableTopologicalSort.Edge<Type>(dependency, entry.SystemType));
            }
        }

        var tieBreak = nodes.Select((t, i) => (t, i)).ToDictionary(x => x.t, x => x.i);
        var order = StableTopologicalSort.Sort(nodes, edges, tieBreak, t => t.Name);
        return [.. order.Select(t => byType[t])];
    }

    /// <summary>
    /// Registers <paramref name="instance"/> as the <typeparamref name="T"/> resource on the
    /// <see cref="World"/> this builder produces. Throws immediately if <typeparamref name="T"/>
    /// is already registered on this builder, the same way a duplicate <c>AddSystem&lt;T&gt;()</c>
    /// does.
    /// </summary>
    public WorldBuilder AddResource<T>(T instance) where T : struct, IResource
    {
        ThrowIfAlreadyBuilt();
        RegisterResource(typeof(T), world => world.AddResource(instance));
        return this;
    }

    /// <summary>Same as <see cref="AddResource{T}(T)"/>, but builds the value from a factory that receives the new <see cref="World"/>.</summary>
    public WorldBuilder AddResource<T>(Func<World, T> factory) where T : struct, IResource
    {
        ThrowIfAlreadyBuilt();
        RegisterResource(typeof(T), world => world.AddResource(factory));
        return this;
    }

    private void RegisterResource(Type resourceType, Action<World> apply)
    {
        if (_pendingResources.Exists(r => r.ResourceType == resourceType))
            throw new InvalidOperationException(
                $"A resource of type '{resourceType}' is already registered on this WorldBuilder. At most one instance per resource Type is supported.");
        _pendingResources.Add((resourceType, apply));
    }

    /// <summary>
    /// Registers one system, deferring its construction until <see cref="Build"/> (so a
    /// <c>ctor(World)</c> can receive the <see cref="World"/> being built). Not called
    /// directly by consumer code: the generator emits a strongly-typed
    /// <c>AddSystem&lt;T&gt;()</c> overload closing over this, resolving
    /// <paramref name="access"/>/<paramref name="construct"/>/<paramref name="generatedBeforeTargets"/>/
    /// <paramref name="generatedAfterTargets"/> from <c>Wyrd.Ecs.Generated.SystemRegistry</c>
    /// so none of them are ever spelled out by hand. <paramref name="generatedBeforeTargets"/>/
    /// <paramref name="generatedAfterTargets"/> seed the entry's edges (from
    /// <c>[RunBefore]</c>/<c>[RunAfter]</c>); further <see cref="SystemRegistration.Before{T}"/>/
    /// <see cref="SystemRegistration.After{T}"/> calls union in on top. Seeding happens here,
    /// not in the generated extension method, because <see cref="SystemRegistration"/> exposes
    /// its underlying <see cref="SystemEntry"/> only as `internal`, unreachable from the
    /// consumer assembly generated code lives in. Returns a chainable
    /// <see cref="SystemRegistration"/> for declaring further ordering edges or starting the
    /// system disabled. Throws if a system of <paramref name="systemType"/> is already
    /// registered on this builder (same at-most-one-instance rule
    /// <see cref="ParallelSystemScheduler.Register"/> enforces at runtime, checked here too so
    /// a duplicate is diagnosed at the <c>AddSystem&lt;T&gt;()</c> call site, not later).
    /// <paramref name="constructionDependencies"/> declares which other registered types must
    /// be constructed first, resolved by <see cref="Build"/> independent of call order - see
    /// <see cref="SystemEntry.ConstructionDependencies"/>. Trailing and optional, defaulting
    /// to none, so the generator's positional <c>AddSystem&lt;T&gt;()</c> call sites keep
    /// compiling unchanged; only a hand-written registration that actually has a construction
    /// dependency (e.g. <c>Wyrd.Ecs.Renderer</c>'s <c>AddRenderer</c> on <c>PlatformSystem</c>)
    /// needs to name it.
    /// </summary>
    public SystemRegistration AddSystemCore(
        Type systemType,
        SystemAccess? access,
        Func<World, EcsSystem> construct,
        IReadOnlyList<Type> generatedBeforeTargets,
        IReadOnlyList<Type> generatedAfterTargets,
        SystemCadence cadence = SystemCadence.Variable,
        IReadOnlyList<Type>? constructionDependencies = null)
    {
        ThrowIfAlreadyBuilt();
        var entry = RegisterEntry(systemType, access, construct, generatedBeforeTargets, generatedAfterTargets, cadence);
        entry.ConstructionDependencies.AddRange(constructionDependencies ?? []);
        return new SystemRegistration(RegisterEntry, Build, entry);
    }

    private SystemEntry RegisterEntry(
        Type systemType,
        SystemAccess? access,
        Func<World, EcsSystem> construct,
        IReadOnlyList<Type> generatedBeforeTargets,
        IReadOnlyList<Type> generatedAfterTargets,
        SystemCadence cadence = SystemCadence.Variable)
    {
        ThrowIfAlreadyBuilt();
        if (_pending.Exists(e => e.SystemType == systemType))
            throw new InvalidOperationException(
                $"A system of type '{systemType}' is already registered on this WorldBuilder. At most one instance per system Type is supported.");

        var entry = new SystemEntry { SystemType = systemType, Construct = construct, Access = access, Cadence = cadence };
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
        ThrowIfAlreadyBuilt();
        _parallelThreshold = entityCount;
        return this;
    }

    /// <summary>
    /// Overrides the default <see cref="ParallelSystemScheduler"/> - the extensibility
    /// hook for a custom <see cref="ISystemScheduler"/> (e.g. a deterministic,
    /// non-parallel implementation for lockstep/replay netcode).
    /// </summary>
    public WorldBuilder WithScheduler(ISystemScheduler scheduler)
    {
        ThrowIfAlreadyBuilt();
        _scheduler = scheduler;
        return this;
    }

    /// <summary>
    /// Configures the interval and catch-up bound for <see cref="SystemCadence.Fixed"/>
    /// systems. Defaults to <c>1/60s</c> and <c>5</c> substeps if never called: a
    /// <see cref="FixedTimestepAttribute"/> system works out of the box without this call.
    /// <paramref name="maxSubstepsPerUpdate"/> bounds how many fixed steps a single
    /// <see cref="World.Update"/> call can run: the accumulator itself is clamped to at most
    /// <c>maxSubstepsPerUpdate * step</c> after each call's contribution is added, so a
    /// backlog from one slow frame can never grow across repeated slow calls. Excess
    /// accumulated time is dropped, not deferred.
    /// </summary>
    public WorldBuilder WithFixedTimestep(TimeSpan step, int maxSubstepsPerUpdate = 5)
    {
        ThrowIfAlreadyBuilt();
        if (step <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(step), step, "Fixed timestep must be positive.");
        if (maxSubstepsPerUpdate <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxSubstepsPerUpdate), maxSubstepsPerUpdate, "maxSubstepsPerUpdate must be positive.");

        _fixedStep = step;
        _maxSubstepsPerUpdate = maxSubstepsPerUpdate;
        return this;
    }

    /// <summary>
    /// Bounds the raw delta any call to <see cref="World.Update"/> can pass on to the
    /// fixed-step accumulator and <see cref="World.RealTime"/> alike, clamped before either
    /// sees it. Defaults to <c>250ms</c>, matching Bevy's <c>Time&lt;Virtual&gt;::set_max_delta</c>
    /// default - the only documented default among the surveyed engines that clamp raw delta
    /// itself (Unity's <c>Time.maximumDeltaTime</c> only bounds fixed-step catch-up). Protects
    /// a debugger breakpoint, a window drag-resize stall, or any other multi-second hitch from
    /// reaching gameplay code as one enormous <see cref="Time.Delta"/> - applies to every
    /// caller of <see cref="World.Update"/>, not just <see cref="World.Run"/>.
    /// </summary>
    public WorldBuilder WithMaxDelta(TimeSpan maxDelta)
    {
        ThrowIfAlreadyBuilt();
        if (maxDelta <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(maxDelta), maxDelta, "Max delta must be positive.");

        _maxDelta = maxDelta;
        return this;
    }

    /// <summary>
    /// Every <see cref="SystemEntry"/> this builder accumulates is a mutable object
    /// (<see cref="SystemEntry.Instance"/> gets written the moment it's actually
    /// constructed) that the resulting <see cref="World"/>'s <see cref="ISystemScheduler"/>
    /// then holds by reference, not by copy. Allowing a second <see cref="Build"/> call
    /// (or any further configuration after one) would mean re-running
    /// <see cref="SystemEntry.Construct"/> against those same shared objects for a new
    /// <see cref="World"/>, silently overwriting <see cref="SystemEntry.Instance"/> out
    /// from under the *first* World's scheduler - no exception, just a system quietly
    /// pointing at the wrong World from then on. Throwing here instead makes a
    /// <see cref="WorldBuilder"/> strictly single-use: construct one, configure it, call
    /// <see cref="Build"/> once, discard it.
    /// </summary>
    private void ThrowIfAlreadyBuilt()
    {
        if (_built)
            throw new InvalidOperationException("This WorldBuilder has already built a World. Each WorldBuilder is single-use - create a new one for another World.");
    }
}
