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
public sealed class ParallelSystemScheduler : ISystemScheduler
{
    private readonly int _parallelThreshold;
    private readonly List<SystemEntry> _entries = [];
    private IReadOnlyList<IReadOnlyList<EcsSystem>> _stages = [];
    private bool _dirty;

    public ParallelSystemScheduler(int parallelThreshold) => _parallelThreshold = parallelThreshold;

    /// <inheritdoc/>
    public SystemRegistration Register(SystemEntry entry, World world)
    {
        RegisterConstructed(entry, world);
        return new SystemRegistration(RegisterFromParts(world), build: null, entry);
    }

    /// <inheritdoc/>
    public void InitialRegister(IReadOnlyList<SystemEntry> entries, World world)
    {
        foreach (var entry in entries)
        {
            entry.Instance = entry.Construct(world);
            entry.Instance.Enabled = entry.StartEnabled;
        }
        _entries.AddRange(entries);
        Recompute();
    }

    /// <inheritdoc/>
    public bool Remove(EcsSystem system)
    {
        var index = _entries.FindIndex(e => ReferenceEquals(e.Instance, system));
        if (index < 0) return false;

        _entries.RemoveAt(index);
        _dirty = true;
        return true;
    }

    /// <inheritdoc/>
    public EcsSystem? Find(Type systemType) =>
        _entries.FirstOrDefault(e => e.SystemType == systemType)?.Instance;

    /// <inheritdoc/>
    public void RunStages(World world, Time time)
    {
        if (_dirty) { Recompute(); _dirty = false; }

        foreach (var stage in _stages)
        {
            if (stage.Count > 1 && world.TotalEntityCount >= _parallelThreshold)
                System.Threading.Tasks.Parallel.ForEach(stage, system => { if (system.Enabled) system.InvokeExecute(world, time); });
            else
                foreach (var system in stage) { if (system.Enabled) system.InvokeExecute(world, time); }

            world.ApplyCommands();
        }
    }

    private SystemEntry RegisterConstructed(SystemEntry entry, World world)
    {
        entry.Instance = entry.Construct(world);
        entry.Instance.Enabled = entry.StartEnabled;
        _entries.Add(entry);
        _dirty = true;
        return entry;
    }

    /// <summary>Adapter matching <see cref="SystemRegistration"/>'s stored delegate shape, so a chained <c>.AddSystem&lt;T&gt;()</c> off a runtime registration keeps registering onto this same scheduler.</summary>
    private Func<Type, SystemAccess?, Func<World, EcsSystem>, IReadOnlyList<Type>, IReadOnlyList<Type>, SystemEntry> RegisterFromParts(World world) =>
        (systemType, access, construct, before, after) =>
        {
            var next = new SystemEntry { SystemType = systemType, Construct = construct, Access = access };
            next.BeforeTargets.AddRange(before);
            next.AfterTargets.AddRange(after);
            return RegisterConstructed(next, world);
        };

    private void Recompute() => _stages = Internal.StagePlanner.BuildStages(_entries);
}
