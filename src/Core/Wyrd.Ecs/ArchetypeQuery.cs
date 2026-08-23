using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs;

/// <summary>
/// The lowest-level query primitive: an archetype-matching filter that resolves against a
/// <see cref="World"/> as <see cref="ArchetypeChunks"/>, each offering direct
/// <see cref="ArchetypeChunk.Access{TAccessor}"/> span access to component columns. Every
/// other query surface is built on this one. Use <see cref="Has{T}"/> for presence-only
/// checks; use <see cref="Access{TAccessor}"/> for a type a chunk will actually read or write.
///
/// <para>
/// Every query originates from <see cref="Empty"/> via its Has/Access/Without/Any builders;
/// <c>default(ArchetypeQuery)</c> is not a meaningful value - the filter it carries never ran
/// its constructor-computed identity hash, so it compares unequal to an equal-shaped query.
/// </para>
/// </summary>
public readonly partial struct ArchetypeQuery : IEquatable<ArchetypeQuery>
{
    /// <summary>An empty query: matches every archetype in the world.</summary>
    public static readonly ArchetypeQuery Empty = new(ArchetypeFilter.Empty);

    private readonly ArchetypeFilter _filter;

    private ArchetypeQuery(ArchetypeFilter filter) => _filter = filter;

    /// <summary>Requires the archetype to contain <typeparamref name="T"/>. Never yields an accessor: <typeparamref name="T"/>'s data is not read.</summary>
    public ArchetypeQuery Has<T>() where T : struct => new(_filter.Has<T>());

    /// <summary>Requires the archetype to contain <typeparamref name="TAccessor"/>'s component type, readying it for <see cref="ArchetypeChunk.Access{TAccessor}"/> on every resolved chunk.</summary>
    public ArchetypeQuery Access<TAccessor>() where TAccessor : struct, IComponentAccessor<TAccessor>, allows ref struct =>
        new(_filter.HasIndex(TAccessor.TypeIndex));

    /// <summary>Requires the archetype to NOT contain <typeparamref name="T"/>.</summary>
    public ArchetypeQuery Without<T>() where T : struct => new(_filter.Without<T>());

    /// <summary>Requires the archetype to contain at least one of <typeparamref name="T0"/>/<typeparamref name="T1"/>.</summary>
    public ArchetypeQuery Any<T0, T1>() where T0 : struct where T1 : struct => new(_filter.Any<T0, T1>());

    /// <summary>
    /// Combines this query's filter with <paramref name="other"/>'s: every requirement from
    /// both must hold. See <see cref="ArchetypeFilter.Combine"/>.
    /// </summary>
    public ArchetypeQuery Combine(ArchetypeQuery other) => new(_filter.Combine(other._filter));

    /// <inheritdoc/>
    public bool Equals(ArchetypeQuery other) => _filter.Equals(other._filter);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ArchetypeQuery other && Equals(other);

    /// <inheritdoc/>
    // The filter's own hash is computed once at its construction; delegating keeps Equal
    // queries hash-identical to it without re-walking the filter's groups per comparison.
    public override int GetHashCode() => _filter.GetHashCode();

    /// <summary>Every archetype in <paramref name="world"/> currently matching this query.</summary>
    public ArchetypeChunks Resolve(World world) =>
        new(world.GetMatchingArchetypes(TypeBitSet.Empty, _filter), world);

    /// <summary>
    /// Resolves this query with <paramref name="additional"/>'s constraints layered on top,
    /// without materializing the combined filter: generated terminals call this once per
    /// invocation, so their backend terms and the caller's chain filter are probed as a pair
    /// in the archetype-set cache instead of being recombined every call. Public because
    /// generated code compiles into arbitrary consumer assemblies with no
    /// <c>InternalsVisibleTo</c> grant - not intended for hand-written call sites, which
    /// should keep chaining filters and calling <see cref="Resolve(World)"/>.
    /// </summary>
    public ArchetypeChunks Resolve(World world, ArchetypeQuery additional) =>
        new(world.GetMatchingArchetypes(_filter, additional._filter), world);
}
