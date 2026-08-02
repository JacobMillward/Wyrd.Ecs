namespace Wyrd.Ecs;

/// <summary>
/// Runs the parallel schedule built by <see cref="WorldBuilder.Build"/>. Each stage
/// dispatches inline or to the thread pool based on <see cref="World.TotalEntityCount"/>
/// versus <see cref="WorldBuilder.WithParallelThreshold"/>, then flushes
/// <see cref="World.Commands"/> once every system in the stage has returned.
/// </summary>
public sealed class ScheduledExecutor
{
    private readonly IReadOnlyList<IReadOnlyList<EcsSystem>> _stages;
    private readonly int _parallelThreshold;

    internal ScheduledExecutor(IReadOnlyList<IReadOnlyList<EcsSystem>> stages, int parallelThreshold)
    {
        _stages = stages;
        _parallelThreshold = parallelThreshold;
    }

    /// <summary>Runs every stage once, in order, applying <see cref="World.Commands"/> at each stage's boundary. Called only by <see cref="World.Update"/>.</summary>
    internal void RunStages(World world, Time time)
    {
        foreach (var stage in _stages)
        {
            if (stage.Count > 1 && world.TotalEntityCount >= _parallelThreshold)
                System.Threading.Tasks.Parallel.ForEach(stage, system => { if (system.Enabled) system.InvokeExecute(world, time); });
            else
                foreach (var system in stage) { if (system.Enabled) system.InvokeExecute(world, time); }

            world.ApplyCommands();
        }
    }
}
