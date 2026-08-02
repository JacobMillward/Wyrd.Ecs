namespace Wyrd.Ecs;

/// <summary>
/// Runs whatever's registered against a <see cref="World"/> for one tick. The
/// extensibility seam <see cref="WorldBuilder.WithScheduler"/> swaps — a custom
/// implementation (e.g. strictly sequential, for deterministic lockstep/replay netcode)
/// drops in without needing any other change to <see cref="World"/>/<see cref="WorldBuilder"/>.
/// <see cref="ParallelSystemScheduler"/> is the default.
/// </summary>
public interface ISystemScheduler
{
    /// <summary>Runs one iteration of every registered system, applying <see cref="World.Commands"/> at whatever stage boundaries this implementation defines. Called only by <see cref="World.Update"/>.</summary>
    void RunStages(World world, Time time);

    /// <summary>
    /// Replaces the schedule <see cref="RunStages"/> runs. Called exactly once, by
    /// <see cref="WorldBuilder.Build"/>, after every registered system's instance
    /// exists (construction needs <see cref="World"/> to already exist for a
    /// <c>ctor(World)</c> system, so the schedule — which needs those instances — can
    /// only be computed afterward).
    /// </summary>
    void AttachStages(IReadOnlyList<IReadOnlyList<EcsSystem>> stages);
}
