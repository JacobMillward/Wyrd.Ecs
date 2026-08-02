namespace Wyrd.Ecs.Tests;

struct CustomSchedulerData : IComponent;

sealed class CustomSchedulerRecordingSystem : EcsSystem
{
    public int ExecuteCallCount;
    protected override void Execute(World world, Time time) => ExecuteCallCount++;
}

/// <summary>
/// A minimal, strictly sequential <see cref="ISystemScheduler"/> — no parallel dispatch,
/// no per-stage thread pool decision. Proves <see cref="ParallelSystemScheduler"/> isn't
/// hardcoded anywhere in <see cref="World"/>/<see cref="WorldBuilder"/>.
/// </summary>
sealed class SequentialScheduler : ISystemScheduler
{
    private IReadOnlyList<IReadOnlyList<EcsSystem>> _stages = [];

    public void AttachStages(IReadOnlyList<IReadOnlyList<EcsSystem>> stages) => _stages = stages;

    public void RunStages(World world, Time time)
    {
        foreach (var stage in _stages)
        {
            foreach (var system in stage)
                if (system.Enabled) system.InvokeExecute(world, time);

            world.ApplyCommands();
        }
    }
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
