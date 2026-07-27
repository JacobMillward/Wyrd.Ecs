namespace Wyrd.Ecs.Generators;

/// <summary>
/// One discovered `QuerySystem` subclass: its containing namespace (empty string for
/// the global namespace), simple class name, and the shape extracted from its `Build`
/// method's return type. Only top-level classes are supported — a `QuerySystem`
/// subclass nested inside another type has no candidate produced for it at all (see
/// <see cref="QueryChainGenerator"/>'s `Initialize`), since re-declaring a matching
/// nested `partial` chain correctly is out of scope for this design.
/// </summary>
internal sealed class QuerySystemCandidate : IEquatable<QuerySystemCandidate>
{
    public required string Namespace { get; init; }
    public required string ClassName { get; init; }
    public required QueryShape Shape { get; init; }

    /// <summary>Value equality, for the same reason -- and via the same <see cref="QueryShape"/> equality -- as <see cref="QueryShape"/> itself: this type also flows through <c>QueryChainGenerator</c>'s incremental pipeline.</summary>
    public bool Equals(QuerySystemCandidate? other) =>
        other is not null && Namespace == other.Namespace && ClassName == other.ClassName && Shape.Equals(other.Shape);

    public override bool Equals(object? obj) => obj is QuerySystemCandidate other && Equals(other);

    /// <summary>Manual combine, not <c>System.HashCode</c> -- this project targets netstandard2.0, where that type doesn't exist.</summary>
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = Namespace.GetHashCode();
            hash = hash * 31 + ClassName.GetHashCode();
            hash = hash * 31 + Shape.GetHashCode();
            return hash;
        }
    }
}
