using System.Collections.Immutable;

namespace Wyrd.Ecs.Generators;

/// <summary>One `[Resource]`-tagged property on a `QuerySystem`: its name, resource type, and whether its setter is public (write-back-enabled) or not (read-only).</summary>
internal readonly record struct ResourcePropertyInfo(string PropertyName, string ResourceTypeName, bool IsWrite);

/// <summary>
/// One discovered `QuerySystem` subclass: its containing namespace (empty for the global
/// namespace), simple class name, and the shape extracted from its `Build` method's
/// return type. Only top-level classes are supported.
/// </summary>
internal sealed class QuerySystemCandidate : IEquatable<QuerySystemCandidate>
{
    public required string Namespace { get; init; }
    public required string ClassName { get; init; }
    public required QueryShape Shape { get; init; }
    public required bool HasWorldParameter { get; init; }
    public required bool HasEntityViewParameter { get; init; }
    public required ImmutableArray<ResourcePropertyInfo> ResourceProperties { get; init; }

    /// <summary>Value equality, for the same reason (and via the same <see cref="QueryShape"/> equality) as <see cref="QueryShape"/> itself: this type also flows through <c>QueryChainGenerator</c>'s incremental pipeline.</summary>
    public bool Equals(QuerySystemCandidate? other) =>
        other is not null
        && Namespace == other.Namespace
        && ClassName == other.ClassName
        && Shape.Equals(other.Shape)
        && HasWorldParameter == other.HasWorldParameter
        && HasEntityViewParameter == other.HasEntityViewParameter
        && ResourceProperties.SequenceEqual(other.ResourceProperties);

    public override bool Equals(object? obj) => obj is QuerySystemCandidate other && Equals(other);

    public override int GetHashCode() =>
        StableHashCode.Start(Namespace).Add(ClassName).Add(Shape).Add(HasWorldParameter).Add(HasEntityViewParameter).AddEach(ResourceProperties);
}
