namespace Wyrd.Ecs;

/// <summary>
/// Applied to a component struct: any <c>[FixedTimestep]</c> system that writes this
/// component automatically gets ordered after <paramref name="target"/> in the same
/// fixed step, with no edge declared on the writing system itself. The query-chain
/// generator discovers this the same way it discovers <see cref="RunBeforeAttribute"/>/
/// <see cref="RunAfterAttribute"/>, at compile time. Only applies to
/// <c>[FixedTimestep]</c> writers. A Variable-cadence system writing this component
/// gets no synthetic edge, since a Fixed/Variable ordering edge throws at
/// <see cref="WorldBuilder.Build"/> time and this attribute must never cause that.
/// </summary>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class RequiresSnapshotBeforeAttribute(Type target) : Attribute
{
    /// <summary>The system type that must run first, in an earlier stage, within the same fixed step.</summary>
    public Type Target { get; } = target;
}
