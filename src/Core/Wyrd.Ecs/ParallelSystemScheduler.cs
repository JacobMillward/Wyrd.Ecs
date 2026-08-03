namespace Wyrd.Ecs;

/// <summary>
/// Runs the parallel schedule built by <see cref="WorldBuilder.Build"/>. Each stage
/// dispatches inline or to the thread pool based on <see cref="World.TotalEntityCount"/>
/// versus <see cref="WorldBuilder.WithParallelThreshold"/>, then flushes
/// <see cref="World.Commands"/> once every system in the stage has returned. The default
/// <see cref="ISystemScheduler"/> — <see cref="WorldBuilder.WithScheduler"/> swaps in a
/// different one. Owns the live registration list directly: <see cref="Register"/>/
/// <see cref="Remove"/> mutate it and mark the schedule dirty; <see cref="RunStages"/>
/// recomputes at most once per call, no matter how many structural changes happened
/// since the previous one.
/// </summary>
/// <remarks>
/// Registration state (<c>_entriesByType</c>/<c>_entries</c>/<c>_dirty</c>) is guarded by
/// <c>_lock</c> because <see cref="RunStages"/> can invoke more than one system's
/// <see cref="EcsSystem.Execute"/> concurrently (the <c>Parallel.ForEach</c> branch below)
/// — if two of those systems both call <c>World.AddSystem</c>/<c>RemoveSystem</c> from
/// within their own <c>Execute</c>, that's a real, supported scenario, not a hypothetical
/// one. The lock is never held across the actual system-execution loop: <see cref="RunStages"/>
/// only takes it briefly, once, to check <c>_dirty</c> and snapshot the current
/// <c>_stages</c> reference into a local variable before releasing it, so a
/// <see cref="Register"/>/<see cref="Remove"/> call made *during* this tick's execution
/// (from any thread) can proceed immediately without contending with — or being able to
/// affect — the stage list this tick is already committed to running.
/// </remarks>
public sealed class ParallelSystemScheduler : ISystemScheduler
{
    private readonly int _parallelThreshold;
    private readonly Lock _lock = new();
    private readonly Dictionary<Type, SystemEntry> _entriesByType = [];
    private readonly List<SystemEntry> _entries = [];
    private IReadOnlyList<IReadOnlyList<EcsSystem>> _stages = [];
    private bool _dirty;

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
            Recompute();
            _dirty = false; // Build() hands back a World that's immediately ready to run, not merely marked dirty for the first Update() to discover.
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
            _dirty = true;
            return true;
        }
    }

    /// <inheritdoc/>
    public EcsSystem? Find(Type systemType)
    {
        lock (_lock) return _entriesByType.TryGetValue(systemType, out var entry) ? entry.Instance : null;
    }

    /// <inheritdoc/>
    public void RunStages(World world, Time time)
    {
        IReadOnlyList<IReadOnlyList<EcsSystem>> stages;
        lock (_lock)
        {
            if (_dirty) { Recompute(); _dirty = false; }
            stages = _stages;
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
            if (_dirty) { Recompute(); _dirty = false; }
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
        _dirty = true;
    }

    /// <summary>Adapter matching <see cref="SystemRegistration"/>'s stored delegate shape, so a chained <c>.AddSystem&lt;T&gt;()</c> off a runtime registration keeps registering onto this same scheduler.</summary>
    private Func<Type, SystemAccess?, Func<World, EcsSystem>, IReadOnlyList<Type>, IReadOnlyList<Type>, SystemEntry> RegisterFromParts(World world) =>
        (systemType, access, construct, before, after) =>
        {
            var next = new SystemEntry { SystemType = systemType, Construct = construct, Access = access };
            next.BeforeTargets.AddRange(before);
            next.AfterTargets.AddRange(after);
            lock (_lock) RegisterLocked(next, world);
            return next;
        };

    /// <summary>Must be called with <c>_lock</c> already held.</summary>
    private void Recompute() => _stages = Internal.StagePlanner.BuildStages(_entries);
}
