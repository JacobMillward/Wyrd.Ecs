namespace Wyrd.Ecs;

/// <summary>
/// Runs the parallel schedule built by <see cref="WorldBuilder.Build"/>. Each stage
/// dispatches inline or to the thread pool based on <see cref="World.TotalEntityCount"/>
/// versus <see cref="WorldBuilder.WithParallelThreshold"/>, then flushes
/// <see cref="World.Commands"/> once every system in the stage has returned. The default
/// <see cref="ISystemScheduler"/>; <see cref="WorldBuilder.WithScheduler"/> swaps in a
/// different one. Owns the live registration list directly: <see cref="Register"/>/
/// <see cref="Remove"/> mutate it and mark the schedule dirty, and <see cref="RunStages"/>
/// recomputes at most once per call regardless of how many structural changes happened
/// since the previous one.
/// </summary>
/// <remarks>
/// Registration state is guarded by <c>_lock</c> because <see cref="RunStages"/> can invoke
/// more than one system's <see cref="EcsSystem.Execute"/> concurrently, and a system calling
/// <c>World.AddSystem</c>/<c>RemoveSystem</c> from within its own <c>Execute</c> is a
/// supported scenario. The lock is never held across the execution loop itself:
/// <see cref="RunStages"/> takes it only briefly, to check <c>_dirty</c> and snapshot the
/// current <c>_stages</c> reference before releasing it, so a concurrent
/// <see cref="Register"/>/<see cref="Remove"/> call can proceed without affecting the stage
/// list this tick already committed to running.
/// </remarks>
public sealed class ParallelSystemScheduler : ISystemScheduler
{
    private readonly int _parallelThreshold;
    private readonly Lock _lock = new();
    private readonly Dictionary<Type, SystemEntry> _entriesByType = [];
    private readonly List<SystemEntry> _entries = [];
    private IReadOnlyList<IReadOnlyList<EcsSystem>> _fixedStages = [];
    private IReadOnlyList<IReadOnlyList<EcsSystem>> _variableStages = [];
    private bool _fixedDirty;
    private bool _variableDirty;

    /// <summary>Starts with an empty schedule — the first <see cref="InitialRegister"/>/<see cref="Register"/> call populates it. <paramref name="parallelThreshold"/> is the minimum <see cref="World.TotalEntityCount"/> a stage needs before <see cref="RunStages"/> dispatches it to the thread pool instead of running it inline.</summary>
    public ParallelSystemScheduler(int parallelThreshold) => _parallelThreshold = parallelThreshold;

    /// <inheritdoc/>
    public SystemRegistration Register(SystemEntry entry, World world)
    {
        lock (_lock) RegisterLocked(entry, world);
        return new SystemRegistration(RegisterFromParts(world), build: null, entry);
    }

    /// <inheritdoc/>
    public void InitialRegister(IReadOnlyList<SystemEntry> entries, World world)
    {
        lock (_lock)
        {
            foreach (var entry in entries)
                RegisterLocked(entry, world);
            var allTypes = AllRegisteredTypes();
            RecomputeFixed(allTypes);
            RecomputeVariable(allTypes);
            // Build() hands back a World that's immediately ready to run, not merely marked dirty for the first Update() to discover.
            _fixedDirty = false;
            _variableDirty = false;
        }
    }

    /// <inheritdoc/>
    public bool Remove(EcsSystem system)
    {
        lock (_lock)
        {
            if (!_entriesByType.TryGetValue(system.GetType(), out var entry) || !ReferenceEquals(entry.Instance, system))
                return false;

            _entriesByType.Remove(system.GetType());
            _entries.Remove(entry);
            if (entry.Cadence == SystemCadence.Fixed) _fixedDirty = true; else _variableDirty = true;
            return true;
        }
    }

    /// <inheritdoc/>
    public EcsSystem? Find(Type systemType)
    {
        lock (_lock) return _entriesByType.TryGetValue(systemType, out var entry) ? entry.Instance : null;
    }

    /// <inheritdoc/>
    public void RunStages(World world, Time time, SystemCadence which)
    {
        IReadOnlyList<IReadOnlyList<EcsSystem>> stages;
        lock (_lock)
        {
            if (which == SystemCadence.Fixed)
            {
                if (_fixedDirty) { RecomputeFixed(AllRegisteredTypes()); _fixedDirty = false; }
                stages = _fixedStages;
            }
            else
            {
                if (_variableDirty) { RecomputeVariable(AllRegisteredTypes()); _variableDirty = false; }
                stages = _variableStages;
            }
        }

        foreach (var stage in stages)
        {
            if (stage.Count > 1 && world.TotalEntityCount >= _parallelThreshold)
                System.Threading.Tasks.Parallel.ForEach(stage, system => { if (system.Enabled) system.InvokeExecute(world, time); });
            else
                foreach (var system in stage) { if (system.Enabled) system.InvokeExecute(world, time); }

            world.ApplyCommands();
        }
    }

    /// <inheritdoc/>
    public void Flush()
    {
        lock (_lock)
        {
            if (!_fixedDirty && !_variableDirty) return;

            var allTypes = AllRegisteredTypes();
            if (_fixedDirty) { RecomputeFixed(allTypes); _fixedDirty = false; }
            if (_variableDirty) { RecomputeVariable(allTypes); _variableDirty = false; }
        }
    }

    /// <summary>Must be called with <c>_lock</c> already held.</summary>
    private void RegisterLocked(SystemEntry entry, World world)
    {
        if (_entriesByType.ContainsKey(entry.SystemType))
            throw new InvalidOperationException(
                $"A system of type '{entry.SystemType}' is already registered. At most one instance per system Type is supported — GetSystem<T>()/RemoveSystem<T>() and Type-targeted Before<T>()/After<T>() edges all assume it.");

        entry.Instance = entry.Construct(world);
        entry.Instance.Enabled = entry.StartEnabled;
        _entriesByType.Add(entry.SystemType, entry);
        _entries.Add(entry);
        if (entry.Cadence == SystemCadence.Fixed) _fixedDirty = true; else _variableDirty = true;
    }

    /// <summary>Adapter matching <see cref="SystemRegistration"/>'s stored delegate shape, so a chained <c>.AddSystem&lt;T&gt;()</c> off a runtime registration keeps registering onto this same scheduler.</summary>
    private Func<Type, SystemAccess?, Func<World, EcsSystem>, IReadOnlyList<Type>, IReadOnlyList<Type>, SystemCadence, SystemEntry> RegisterFromParts(World world) =>
        (systemType, access, construct, before, after, cadence) =>
        {
            var next = new SystemEntry { SystemType = systemType, Construct = construct, Access = access, Cadence = cadence };
            next.BeforeTargets.AddRange(before);
            next.AfterTargets.AddRange(after);
            lock (_lock) RegisterLocked(next, world);
            return next;
        };

    /// <summary>Must be called with <c>_lock</c> already held. Shared by every caller that recomputes one or both partitions, so a batch that touches both never rebuilds this list twice.</summary>
    private IReadOnlyCollection<Type> AllRegisteredTypes() => _entries.Select(e => e.SystemType).ToList();

    /// <summary>Must be called with <c>_lock</c> already held.</summary>
    private void RecomputeFixed(IReadOnlyCollection<Type> allTypes)
    {
        var fixedEntries = _entries.Where(e => e.Cadence == SystemCadence.Fixed).ToList();
        _fixedStages = Internal.StagePlanner.BuildStages(fixedEntries, allTypes);
    }

    /// <summary>Must be called with <c>_lock</c> already held.</summary>
    private void RecomputeVariable(IReadOnlyCollection<Type> allTypes)
    {
        var variableEntries = _entries.Where(e => e.Cadence == SystemCadence.Variable).ToList();
        _variableStages = Internal.StagePlanner.BuildStages(variableEntries, allTypes);
    }
}
