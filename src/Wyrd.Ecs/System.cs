namespace Wyrd.Ecs;

/// <summary>
/// The entry point every System implements the same way, regardless of whether it
/// uses no query, one query via <see cref="QuerySystem{T0}"/>, or several queries
/// called directly. A scheduler (not built here) discovers, orders, and invokes
/// System instances through this single member.
/// </summary>
public abstract class System
{
    /// <summary>Runs one update. <paramref name="tick"/> is the caller's own tick counter; this type doesn't require it to match <see cref="World.CurrentTick"/>.</summary>
    protected abstract void OnUpdate(World world, ulong tick);

    /// <summary>Test/harness convenience: runs <see cref="OnUpdate"/> once directly, without a scheduler.</summary>
    public void RunOnce(World world, ulong tick) => OnUpdate(world, tick);
}
