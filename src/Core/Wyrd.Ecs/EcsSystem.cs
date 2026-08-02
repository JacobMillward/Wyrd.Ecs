namespace Wyrd.Ecs;

/// <summary>
/// The entry point every system implements. <see cref="World"/> discovers, orders, and
/// invokes system instances through this single member; see <see cref="World.Update"/>
/// and <see cref="World.RunOnce"/>. Named <c>EcsSystem</c>, not <c>System</c>, so a
/// consumer's own <c>using Wyrd.Ecs;</c> never collides with the <c>System</c> namespace.
/// </summary>
public abstract class EcsSystem : SchedulableSystem
{
    /// <summary>
    /// Whether <see cref="ScheduledExecutor.RunStages"/> invokes this system's
    /// <see cref="Execute"/> on the current tick. Cheap and immediate: toggling this
    /// never touches the schedule itself, unlike removing a system entirely. The
    /// recommended default for routine pause/resume.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Runs one iteration. <paramref name="time"/> is built by <see cref="World.Update"/>/
    /// <see cref="World.RunOnce"/> from the caller-supplied delta. It's unrelated to
    /// <see cref="World.CurrentTick"/>, the separate internal counter change-tracking
    /// stamps against.
    /// </summary>
    protected abstract void Execute(World world, Time time);

    /// <summary>
    /// The only way <see cref="World"/>/<see cref="ScheduledExecutor"/> reach
    /// <see cref="Execute"/>. Kept as a plain, non-virtual <c>internal</c> forwarder rather
    /// than making <see cref="Execute"/> itself <c>protected internal</c>, since the required
    /// override modifier for a <c>protected internal</c> member depends on whether the
    /// overriding assembly has an <c>InternalsVisibleTo</c> grant.
    /// </summary>
    internal void InvokeExecute(World world, Time time) => Execute(world, time);

    /// <summary>
    /// Called exactly once, by <see cref="World.RemoveSystem(EcsSystem)"/>, when this
    /// system is removed from a <see cref="World"/>. The constructor is this type's
    /// create hook (it only ever runs once, wherever construction happens); this is its
    /// counterpart for teardown. No-op by default.
    /// </summary>
    protected virtual void OnDestroy() { }

    /// <summary>The only way <see cref="World.RemoveSystem(EcsSystem)"/> reaches <see cref="OnDestroy"/> — same rationale as <see cref="InvokeExecute"/>.</summary>
    internal void InvokeOnDestroy() => OnDestroy();
}
