namespace Wyrd.Ecs;

/// <summary>
/// The entry point every system implements the same way, regardless of whether it
/// uses no query, one query via <see cref="QuerySystem"/>, or several queries
/// called directly. A scheduler (not built here) discovers, orders, and invokes
/// system instances through this single member. Named <c>EcsSystem</c>, not
/// <c>System</c>, so a consumer's own <c>using Wyrd.Ecs;</c> never collides with the
/// <c>System</c> namespace.
/// </summary>
public abstract class EcsSystem
{
    /// <summary>
    /// Runs one iteration. <paramref name="time"/> is built by <see cref="World.Tick"/>/
    /// <see cref="World.RunOnce"/> from the caller-supplied delta — unrelated to
    /// <see cref="World.CurrentTick"/>, the separate internal counter change-tracking
    /// stamps against.
    /// </summary>
    protected abstract void OnUpdate(World world, Time time);

    /// <summary>Test/harness convenience: runs <see cref="OnUpdate"/> once directly, without a scheduler.</summary>
    public void RunOnce(World world, Time time) => OnUpdate(world, time);
}
