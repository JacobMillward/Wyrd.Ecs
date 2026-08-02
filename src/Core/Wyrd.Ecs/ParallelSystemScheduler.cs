namespace Wyrd.Ecs;

/// <summary>
/// Runs the parallel schedule built by <see cref="WorldBuilder.Build"/>. Each stage
/// dispatches inline or to the thread pool based on <see cref="World.TotalEntityCount"/>
/// versus <see cref="WorldBuilder.WithParallelThreshold"/>, then flushes
/// <see cref="World.Commands"/> once every system in the stage has returned. The default
/// <see cref="ISystemScheduler"/> — <see cref="WorldBuilder.WithScheduler"/> swaps in a
/// different one.
/// </summary>
public sealed class ParallelSystemScheduler : ISystemScheduler
{
    private readonly int _parallelThreshold;
    private IReadOnlyList<IReadOnlyList<EcsSystem>> _stages;

    /// <summary>Starts with an empty schedule — see <see cref="AttachStages"/>. Used both as <see cref="World"/>'s parameterless-constructor fallback and by <see cref="WorldBuilder"/> before it has stages to attach.</summary>
    public ParallelSystemScheduler(int parallelThreshold)
        : this([], parallelThreshold)
    {
    }

    internal ParallelSystemScheduler(IReadOnlyList<IReadOnlyList<EcsSystem>> stages, int parallelThreshold)
    {
        _stages = stages;
        _parallelThreshold = parallelThreshold;
    }

    /// <inheritdoc/>
    public void AttachStages(IReadOnlyList<IReadOnlyList<EcsSystem>> stages) => _stages = stages;

    /// <inheritdoc/>
    public void RunStages(World world, Time time)
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
