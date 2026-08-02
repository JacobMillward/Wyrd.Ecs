namespace Wyrd.Ecs;

/// <summary>
/// Entry point for attaching Before/After edges to a system at registration time
/// without editing the system's own class, e.g. a system you don't own, or a one-off
/// reorder. <c>Order.For(system).Before&lt;T&gt;()</c> is additive with
/// <see cref="RunBeforeAttribute"/>/<see cref="RunAfterAttribute"/> declared on the
/// system's class: both surfaces contribute edges, neither overrides the other.
/// </summary>
public static class Order
{
    /// <summary>Starts a fluent edge declaration for <paramref name="system"/>.</summary>
    public static OrderedSystem For(EcsSystem system) => system;
}
