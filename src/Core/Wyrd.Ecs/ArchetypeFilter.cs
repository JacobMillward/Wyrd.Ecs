using System.Collections.Immutable;
using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs;

/// <summary>
/// Presence/absence/any-of matching layered on top of a query's own required component
/// signature. <see cref="AnyGroups"/> holds one entry per independent <c>.Any()</c> call;
/// every group must have at least one matching bit for <see cref="Matches"/> to hold, so
/// <c>.Any&lt;A,B&gt;().Any&lt;C,D&gt;()</c> means "(A or B) AND (C or D)", not just
/// whichever group was added last.
/// </summary>
public readonly partial struct ArchetypeFilter : IEquatable<ArchetypeFilter>
{
    /// <summary>The filter with nothing required, excluded, or any-of'd: matches every archetype.</summary>
    public static readonly ArchetypeFilter Empty = new(TypeBitSet.Empty, TypeBitSet.Empty, ImmutableArray<TypeBitSet>.Empty);

    internal TypeBitSet Required { get; }
    internal TypeBitSet Excluded { get; }
    internal ImmutableArray<TypeBitSet> AnyGroups { get; }

    private ArchetypeFilter(TypeBitSet required, TypeBitSet excluded, ImmutableArray<TypeBitSet> anyGroups)
    {
        Required = required;
        Excluded = excluded;
        AnyGroups = anyGroups;
    }

    /// <summary>Requires the archetype to contain <typeparamref name="T"/>.</summary>
    public ArchetypeFilter Has<T>() where T : struct =>
        HasIndex(TypeIndex<T>.Value);

    /// <summary>Same as <see cref="Has{T}"/>, for a caller that already has the type index (e.g. from <see cref="IComponentAccessor{TSelf}.TypeIndex"/>) rather than the type itself.</summary>
    public ArchetypeFilter HasIndex(int typeIndex) =>
        new(Required.With(typeIndex), Excluded, AnyGroups);

    /// <summary>Requires the archetype to NOT contain <typeparamref name="T"/>.</summary>
    public ArchetypeFilter Without<T>() where T : struct =>
        new(Required, Excluded.With(TypeIndex<T>.Value), AnyGroups);

    /// <summary>
    /// Requires the archetype to contain at least one of <typeparamref name="T0"/>/<typeparamref name="T1"/>.
    /// Adds a new independent group rather than replacing any earlier one, so calling this
    /// more than once ANDs each group's own "any of" requirement together.
    /// </summary>
    public ArchetypeFilter Any<T0, T1>() where T0 : struct where T1 : struct =>
        new(Required, Excluded, AnyGroups.Add(TypeBitSet.Empty.With(TypeIndex<T0>.Value).With(TypeIndex<T1>.Value)));

    /// <summary>True when <paramref name="archetypeSignature"/> satisfies every requirement: has everything in <see cref="Required"/>, nothing in <see cref="Excluded"/>, and at least one bit from every group in <see cref="AnyGroups"/>. Internal, not public: <see cref="TypeBitSet"/> itself stays internal, and the only caller is <see cref="World.GetMatchingArchetypes(TypeBitSet, ArchetypeFilter)"/>, in the same assembly.</summary>
    internal bool Matches(TypeBitSet archetypeSignature) =>
        Required.IsSubsetOf(archetypeSignature)
        && !Excluded.Intersects(archetypeSignature)
        && AnyGroups.All(group => group.Intersects(archetypeSignature));

    /// <summary>Combines this filter with <paramref name="other"/>: the union of both <see cref="Required"/> sets, the union of both <see cref="Excluded"/> sets, and every <see cref="AnyGroups"/> entry from both. Every constraint from both filters must hold.</summary>
    public ArchetypeFilter Combine(ArchetypeFilter other) =>
        new(Required.Union(other.Required), Excluded.Union(other.Excluded), AnyGroups.AddRange(other.AnyGroups));

    /// <inheritdoc/>
    public bool Equals(ArchetypeFilter other) =>
        Required.Equals(other.Required) && Excluded.Equals(other.Excluded) && AnyGroups.SequenceEqual(other.AnyGroups);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ArchetypeFilter other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Required);
        hash.Add(Excluded);
        foreach (var group in AnyGroups) hash.Add(group);
        return hash.ToHashCode();
    }
}
