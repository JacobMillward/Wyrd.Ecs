namespace Wyrd.Ecs;

/// <summary>
/// Runs whatever's registered against a <see cref="World"/> for one tick, and owns the
/// live registration list that produces that schedule. The extensibility seam
/// <see cref="WorldBuilder.WithScheduler"/> swaps — a custom implementation (e.g.
/// strictly sequential, for deterministic lockstep/replay netcode) drops in without
/// needing any other change to <see cref="World"/>/<see cref="WorldBuilder"/>.
/// <see cref="ParallelSystemScheduler"/> is the default. Registration/removal both mark
/// the schedule dirty rather than recomputing immediately — see <see cref="RunStages"/>.
/// </summary>
public interface ISystemScheduler
{
    /// <summary>
    /// Runs one iteration of every registered system, applying <see cref="World.Commands"/>
    /// at whatever stage boundaries this implementation defines. If a structural change
    /// (<see cref="Register"/>/<see cref="Remove"/>) happened since the last call, the
    /// schedule is recomputed once here, first — coalescing any number of such changes
    /// since the last tick into a single recompute, rather than one per call. Called
    /// only by <see cref="World.Update"/>.
    /// </summary>
    void RunStages(World world, Time time);

    /// <summary>
    /// Constructs and registers one system immediately (so <see cref="World.GetSystem{T}"/>
    /// reflects it right away), marking the schedule dirty — actual stage placement is
    /// deferred to the next <see cref="RunStages"/> call. Returns a chainable
    /// <see cref="SystemRegistration"/> for the just-added entry.
    /// </summary>
    SystemRegistration Register(SystemEntry entry, World world);

    /// <summary>
    /// Bulk registration path used once by <see cref="WorldBuilder.Build"/>: constructs
    /// every entry's instance, adds them all, then recomputes stages exactly once — not
    /// once per entry.
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
}
