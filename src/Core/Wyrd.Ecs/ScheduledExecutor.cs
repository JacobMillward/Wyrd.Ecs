namespace Wyrd.Ecs;

/// <summary>
/// Runs the static parallel schedule <see cref="WorldBuilder.Build"/> built (held
/// internally by <see cref="World"/> and driven via <see cref="World.Tick"/>): per
/// stage, dispatch inline or to the thread pool depending on
/// <see cref="World.TotalEntityCount"/> against <see cref="WorldBuilder.WithParallelThreshold"/>,
/// then flush <see cref="World.Commands"/> once at the stage boundary, after every
/// system in that stage has returned, never while one is still running. See the
/// design's "Deviation from the design spec" note (this session's plan refresh) for
/// why structural mutation still flows through the single shared <c>world.Commands</c>
/// buffer rather than a fresh one injected per system.
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

    /// <summary>Runs every stage once, in order, applying <see cref="World.Commands"/> at each stage's boundary. Called only by <see cref="World.Tick"/>.</summary>
    internal void RunTick(World world, Time time)
    {
        foreach (var stage in _stages)
        {
            if (stage.Count > 1 && world.TotalEntityCount >= _parallelThreshold)
                System.Threading.Tasks.Parallel.ForEach(stage, system => system.InvokeExecute(world, time));
            else
                foreach (var system in stage) system.InvokeExecute(world, time);

            world.ApplyCommands();
        }
    }
}
