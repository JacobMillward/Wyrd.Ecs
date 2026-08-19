namespace Wyrd.Ecs;

/// <summary>
/// Runs whatever's registered against a <see cref="World"/> for one tick, and owns the
/// live registration list that produces that schedule. The extensibility seam
/// <see cref="WorldBuilder.WithScheduler"/> swaps: a custom implementation (e.g. strictly
/// sequential, for deterministic lockstep/replay netcode) drops in without needing any
/// other change to <see cref="World"/>/<see cref="WorldBuilder"/>.
/// <see cref="ParallelSystemScheduler"/> is the default. Registration/removal both mark the
/// schedule dirty rather than recomputing immediately; see <see cref="RunStages"/>/
/// <see cref="Flush"/>.
/// </summary>
/// <remarks>
/// A custom implementation must be safe to call <see cref="Register"/>/<see cref="Remove"/>/
/// <see cref="Find"/> from within a system's own <see cref="EcsSystem.Execute"/>, including
/// concurrently from more than one system in the same parallel stage: a system
/// adding/removing another system mid-tick is a supported, expected use, not an edge case
/// to leave undefined.
/// </remarks>
public interface ISystemScheduler
{
    /// <summary>
    /// Runs one iteration of every registered system of <paramref name="which"/> cadence,
    /// applying <see cref="World.Commands"/> at whatever stage boundaries this implementation
    /// defines. Recomputes that cadence's schedule first if dirty, coalescing any number of
    /// <see cref="Register"/>/<see cref="Remove"/> calls since the last tick into one recompute.
    /// The two cadences recompute independently: a change to one never forces a recompute of
    /// the other. Called only by <see cref="World.Update"/>, once per <see cref="SystemCadence.Fixed"/>
    /// sub-step and once for the single <see cref="SystemCadence.Variable"/> pass; accumulator,
    /// clamping, and pause/scale math all live on <see cref="World"/>, not here, so a custom
    /// implementation of this interface never needs to reimplement them.
    /// </summary>
    void RunStages(World world, Time time, SystemCadence which);

    /// <summary>
    /// Constructs and registers one system immediately (so <see cref="World.GetSystem{T}"/>
    /// reflects it right away), marking the schedule dirty — actual stage placement is
    /// deferred to the next <see cref="RunStages"/>/<see cref="Flush"/> call. Returns a
    /// chainable <see cref="SystemRegistration"/> for the just-added entry. Throws
    /// <see cref="InvalidOperationException"/> if a system of <see cref="SystemEntry.SystemType"/>
    /// is already registered — at most one instance per system Type is supported, since
    /// <see cref="Find"/>/<see cref="World.GetSystem{T}"/>/<see cref="World.RemoveSystem{T}"/>
    /// and Type-targeted ordering edges all assume it.
    /// </summary>
    SystemRegistration Register(SystemEntry entry, World world);

    /// <summary>
    /// Bulk registration path used once by <see cref="WorldBuilder.Build"/>: constructs
    /// every entry's instance, adds them all, then recomputes stages exactly once — not
    /// once per entry. Same duplicate-Type rejection as <see cref="Register"/>.
    /// </summary>
    void InitialRegister(IReadOnlyList<SystemEntry> entries, World world);

    /// <summary>
    /// Removes a previously registered system, marking the schedule dirty. Returns false
    /// if <paramref name="system"/> was never registered (or was already removed).
    /// </summary>
    bool Remove(EcsSystem system);

    /// <summary>
    /// The live instance registered for <paramref name="systemType"/>, or null. Reflects
    /// the current registration list immediately — independent of whether a recompute is
    /// pending.
    /// </summary>
    EcsSystem? Find(Type systemType);

    /// <summary>
    /// Forces an immediate recompute if the schedule is currently dirty — otherwise a
    /// no-op. <see cref="RunStages"/> already does this automatically at the start of
    /// every tick; call this directly only when you want validation errors (an ordering
    /// edge naming a type that never registered, a cycle, an ambiguous target) to surface
    /// right after a batch of runtime <see cref="Register"/>/<see cref="Remove"/> calls,
    /// rather than waiting for the next <see cref="World.Update"/>.
    /// </summary>
    void Flush();
}
