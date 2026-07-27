namespace Wyrd.Ecs;

/// <summary>
/// The entry point every system implements the same way, regardless of whether it
/// uses no query, one query via <see cref="QuerySystem"/>, or several queries
/// called directly. <see cref="World"/> discovers, orders, and invokes system
/// instances through this single member — see <see cref="World.Tick"/> and
/// <see cref="World.RunOnce"/>. Named <c>EcsSystem</c>, not <c>System</c>, so a
/// consumer's own <c>using Wyrd.Ecs;</c> never collides with the <c>System</c>
/// namespace.
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

    /// <summary>
    /// The only way <see cref="World"/>/<see cref="ScheduledExecutor"/> reach
    /// <see cref="OnUpdate"/> — a plain, non-virtual <c>internal</c> forwarder, not a
    /// <c>protected internal</c> declaration on <see cref="OnUpdate"/> itself. A
    /// <c>protected internal</c> member, when overridden from a *different* assembly,
    /// requires the override to be declared with whatever accessibility that assembly
    /// actually has to it — plain <c>protected override</c> for an ordinary consumer with
    /// no relationship to <c>Wyrd.Ecs</c>, but <c>protected internal override</c> for one
    /// with an <c>InternalsVisibleTo</c> grant (like <c>Wyrd.Ecs.Tests</c> has) — so the
    /// exact override modifier a generated/hand-written <c>OnUpdate</c> needs would depend
    /// on which kind of consumer it's compiled into (confirmed directly against both cases
    /// before settling on this). Keeping <see cref="OnUpdate"/> plain <c>protected abstract</c>
    /// — the one modifier every override anywhere can always use — and reaching it through
    /// this ordinary <c>internal</c> method (accessible to it trivially, since both live in
    /// the same class) sidesteps that entirely.
    /// </summary>
    internal void InvokeOnUpdate(World world, Time time) => OnUpdate(world, time);
}
