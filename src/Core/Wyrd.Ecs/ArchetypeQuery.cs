using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs;

/// <summary>
/// The lowest-level query primitive: an archetype-matching filter that resolves against a
/// <see cref="World"/> as <see cref="ArchetypeChunks"/>, each offering direct
/// <see cref="ArchetypeChunk.Access{TAccessor}"/> span access to component columns. Every
/// other query surface -- <see cref="World.Query{TAccess0}(ChunkAction{TAccess0})"/> and the
/// generator-emitted unbounded-shape <see cref="Query{TShape}"/> chain alike -- is built on
/// this one, so archetype resolution and chunk access are implemented in exactly one place.
/// <see cref="Has{T}"/> requires presence only; call <see cref="Access{TAccessor}"/> for a
/// type whose data a chunk will actually read or write, since only that also readies the
/// matching <see cref="ArchetypeChunk.Access{TAccessor}"/> call to succeed.
/// </summary>
public sealed partial class ArchetypeQuery
{
    /// <summary>An empty query: matches every archetype in the world.</summary>
    public static readonly ArchetypeQuery Empty = new(ArchetypeFilter.Empty);

    private readonly ArchetypeFilter _filter;

    private ArchetypeQuery(ArchetypeFilter filter) => _filter = filter;

    /// <summary>Requires the archetype to contain <typeparamref name="T"/>. Never yields an accessor -- <typeparamref name="T"/>'s data is not read.</summary>
    public ArchetypeQuery Has<T>() where T : struct => new(_filter.Has<T>());

    /// <summary>Requires the archetype to contain <typeparamref name="TAccessor"/>'s component type, readying it for <see cref="ArchetypeChunk.Access{TAccessor}"/> on every resolved chunk.</summary>
    public ArchetypeQuery Access<TAccessor>() where TAccessor : struct, IComponentAccessor<TAccessor>, allows ref struct =>
        new(_filter.HasIndex(TAccessor.TypeIndex));

    /// <summary>Requires the archetype to NOT contain <typeparamref name="T"/>.</summary>
    public ArchetypeQuery Without<T>() where T : struct => new(_filter.Without<T>());

    /// <summary>Requires the archetype to contain at least one of <typeparamref name="T0"/>/<typeparamref name="T1"/>.</summary>
    public ArchetypeQuery Any<T0, T1>() where T0 : struct where T1 : struct => new(_filter.Any<T0, T1>());

    /// <summary>
    /// Combines this query's filter with <paramref name="other"/>'s — every requirement from
    /// both must hold. See <see cref="ArchetypeFilter.Combine"/>. Every generated
    /// <c>.ForEach</c>/<c>.ParallelForEach</c> terminal calls this on its cached backend with
    /// the caller's <c>Query&lt;TShape&gt;.Filter</c> — the common case is a caller who never
    /// called <c>.Without</c>/<c>.Has</c>/<c>.Any</c> at all, so <paramref name="other"/> is
    /// still the exact <see cref="Empty"/> reference <see cref="Query{TShape}"/>'s
    /// parameterless constructor set it to. <see cref="ArchetypeQuery"/> is a reference type,
    /// so skipping the real combine (and the `new` it would otherwise allocate) for that one
    /// reference is a measured fix, not a speculative one — see
    /// <c>ArchetypeSignatureBenchmarks</c>/the query-filter-runtime-unification plan's Task 6:
    /// without this, every steady-state <c>.ForEach</c> call allocated where it previously
    /// didn't, confirmed via <c>QueryIterationBenchmarks</c> before/after this fast path existed.
    /// </summary>
    public ArchetypeQuery Combine(ArchetypeQuery other) => ReferenceEquals(other, Empty) ? this : new(_filter.Combine(other._filter));

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ArchetypeQuery other && _filter.Equals(other._filter);

    /// <inheritdoc/>
    public override int GetHashCode() => _filter.GetHashCode();

    /// <summary>Every archetype in <paramref name="world"/> currently matching this query.</summary>
    public ArchetypeChunks Resolve(World world) =>
        new(world.GetMatchingArchetypes(ArchetypeSignature.Empty, _filter), world);
}
