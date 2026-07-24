namespace Wyrd.Ecs.Internal;

/// <summary>
/// Presence/absence/any-of matching layered on top of a query's own required
/// component signature. <see cref="Has{T}"/> widens <see cref="Required"/> (no
/// distinction at this layer between "required by generic type argument" and
/// "required via Has&lt;T&gt;()" — both just set a bit); <see cref="Without{T}"/>
/// and <see cref="Any{T0,T1}"/> are checked separately by <see cref="Matches"/>.
/// </summary>
internal readonly struct QueryFilter : IEquatable<QueryFilter>
{
    internal static readonly QueryFilter Empty = new(ArchetypeSignature.Empty, ArchetypeSignature.Empty, ArchetypeSignature.Empty);

    internal ArchetypeSignature Required { get; }
    internal ArchetypeSignature Excluded { get; }
    internal ArchetypeSignature AnyOf { get; }

    private QueryFilter(ArchetypeSignature required, ArchetypeSignature excluded, ArchetypeSignature anyOf)
    {
        Required = required;
        Excluded = excluded;
        AnyOf = anyOf;
    }

    internal QueryFilter Has<T>() where T : struct =>
        new(Required.With(TypeIndex<T>.Value), Excluded, AnyOf);

    internal QueryFilter Without<T>() where T : struct =>
        new(Required, Excluded.With(TypeIndex<T>.Value), AnyOf);

    internal QueryFilter Any<T0, T1>() where T0 : struct where T1 : struct =>
        new(Required, Excluded, ArchetypeSignature.Empty.With(TypeIndex<T0>.Value).With(TypeIndex<T1>.Value));

    internal bool Matches(ArchetypeSignature archetypeSignature) =>
        Required.IsSubsetOf(archetypeSignature)
        && !Excluded.Intersects(archetypeSignature)
        && (AnyOf.Equals(ArchetypeSignature.Empty) || AnyOf.Intersects(archetypeSignature));

    public bool Equals(QueryFilter other) =>
        Required.Equals(other.Required) && Excluded.Equals(other.Excluded) && AnyOf.Equals(other.AnyOf);

    public override bool Equals(object? obj) => obj is QueryFilter other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Required, Excluded, AnyOf);
}
