namespace Wyrd.Ecs;

/// <summary>
/// A system plus the Before/After edges declared on it at registration time via
/// <see cref="Order"/>. The implicit conversion from <see cref="EcsSystem"/> means a
/// system with no edges needs no wrapping — every <c>WithSystems(a, b, c)</c> call
/// that names bare systems compiles exactly as if this type didn't exist.
/// </summary>
public readonly struct OrderedSystem
{
    internal EcsSystem System { get; }
    internal IReadOnlyList<Type> BeforeTargets { get; }
    internal IReadOnlyList<Type> AfterTargets { get; }

    internal OrderedSystem(EcsSystem system)
        : this(system, [], [])
    {
    }

    private OrderedSystem(EcsSystem system, IReadOnlyList<Type> beforeTargets, IReadOnlyList<Type> afterTargets)
    {
        System = system;
        BeforeTargets = beforeTargets;
        AfterTargets = afterTargets;
    }

    /// <summary>Adds "must run before an instance/synthesized marker of <typeparamref name="T"/>."</summary>
    public OrderedSystem Before<T>() where T : SchedulableSystem =>
        new(System, [.. BeforeTargets, typeof(T)], AfterTargets);

    /// <summary>Adds "must run after an instance/synthesized marker of <typeparamref name="T"/>."</summary>
    public OrderedSystem After<T>() where T : SchedulableSystem =>
        new(System, BeforeTargets, [.. AfterTargets, typeof(T)]);

    /// <summary>A bare system with no declared edges converts implicitly, so it needs no wrapping.</summary>
    public static implicit operator OrderedSystem(EcsSystem system) => new(system);
}
