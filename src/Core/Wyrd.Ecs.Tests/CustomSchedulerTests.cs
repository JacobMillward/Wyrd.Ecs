namespace Wyrd.Ecs.Tests;

struct CustomSchedulerData : IComponent;

sealed class CustomSchedulerRecordingSystem : EcsSystem
{
    public int ExecuteCallCount;
    protected override void Execute(World world, Time time) => ExecuteCallCount++;
}

/// <summary>
/// A minimal, strictly sequential <see cref="ISystemScheduler"/> — no parallel dispatch,
/// no per-stage thread pool decision, and (unlike <see cref="ParallelSystemScheduler"/>)
/// no dirty-flag deferral — recomputes immediately on every structural change, since
/// simplicity matters more than that particular optimization for this fixture. Proves
/// <see cref="ParallelSystemScheduler"/> isn't hardcoded anywhere in
/// <see cref="World"/>/<see cref="WorldBuilder"/>.
/// </summary>
sealed class SequentialScheduler : ISystemScheduler
{
    private readonly List<SystemEntry> _entries = [];
    private IReadOnlyList<IReadOnlyList<EcsSystem>> _stages = [];

    public SystemRegistration Register(SystemEntry entry, World world)
    {
        ConstructAndAdd(entry, world);
        return new SystemRegistration(RegisterFromParts(world), build: null, entry);
    }

    public void InitialRegister(IReadOnlyList<SystemEntry> entries, World world)
    {
        foreach (var entry in entries)
            ConstructAndAdd(entry, world, recompute: false);
        Recompute();
    }

    public bool Remove(EcsSystem system)
    {
        var index = _entries.FindIndex(e => ReferenceEquals(e.Instance, system));
        if (index < 0) return false;

        _entries.RemoveAt(index);
        Recompute();
        return true;
    }

    public EcsSystem? Find(Type systemType) => _entries.FirstOrDefault(e => e.SystemType == systemType)?.Instance;

    /// <summary>No-op: this fixture recomputes immediately on every structural change, so there's never anything deferred to flush.</summary>
    public void Flush() { }

    public void RunStages(World world, Time time)
    {
        foreach (var stage in _stages)
        {
            foreach (var system in stage)
                if (system.Enabled) system.InvokeExecute(world, time);

            world.ApplyCommands();
        }
    }

    private void ConstructAndAdd(SystemEntry entry, World world, bool recompute = true)
    {
        entry.Instance = entry.Construct(world);
        entry.Instance.Enabled = entry.StartEnabled;
        _entries.Add(entry);
        if (recompute) Recompute();
    }

    private Func<Type, SystemAccess?, Func<World, EcsSystem>, IReadOnlyList<Type>, IReadOnlyList<Type>, SystemCadence, SystemEntry> RegisterFromParts(World world) =>
        (systemType, access, construct, before, after, cadence) =>
        {
            var next = new SystemEntry { SystemType = systemType, Construct = construct, Access = access, Cadence = cadence };
            next.BeforeTargets.AddRange(before);
            next.AfterTargets.AddRange(after);
            ConstructAndAdd(next, world);
            return next;
        };

    private void Recompute() => _stages = Wyrd.Ecs.Internal.StagePlanner.BuildStages(_entries);
}

public class CustomSchedulerTests
{
    [Fact]
    public void WithScheduler_UsesTheSuppliedSchedulerInsteadOfTheDefault()
    {
        var builder = new WorldBuilder().WithScheduler(new SequentialScheduler());
        CustomSchedulerRecordingSystem? constructed = null;
        builder.AddSystemCore(
            typeof(CustomSchedulerRecordingSystem),
            new SystemAccess(Reads: [], Writes: [typeof(CustomSchedulerData)]),
            _ => constructed = new CustomSchedulerRecordingSystem(),
            [],
            []);
        var world = builder.Build();

        world.Update(TimeSpan.FromSeconds(1));

        constructed!.ExecuteCallCount.Should().Be(1);
    }
}
