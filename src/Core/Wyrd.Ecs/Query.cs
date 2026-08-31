namespace Wyrd.Ecs;

/// <summary>
/// The chain's entry point, before any <c>.With</c> call has picked a shape: the
/// non-generic counterpart of <see cref="Query{TShape}"/>, which only exists from the
/// first <c>.With</c> call onward. Kept separate (rather than just <c>Query&lt;Nil&gt;</c>)
/// so <c>QuerySystem.DefineQuery</c> and every other entry-point signature can read as
/// plain <c>Query</c> instead of spelling out the empty-shape marker. Arity-2+
/// <c>With</c>/<c>Has</c>/<c>Without</c>/<c>Any</c> overloads are generated the same way as
/// <see cref="Query{TShape}"/>'s, see <c>WorldQueryMembersGenerator</c>.
/// </summary>
public readonly partial struct Query : IQuery
{
    /// <inheritdoc cref="Query{TShape}.World"/>
    public readonly World World;

    /// <inheritdoc cref="Query{TShape}.Filter"/>
    public readonly ArchetypeQuery Filter;

    internal Query(World world) : this(world, ArchetypeQuery.Empty) { }

    internal Query(World world, ArchetypeQuery filter)
    {
        World = world;
        Filter = filter;
    }

    /// <inheritdoc cref="Query{TShape}.With{TComponent}"/>
    public Query<(TComponent, Nil)> With<TComponent>() where TComponent : struct, IComponent => new(World, Filter);

    /// <inheritdoc cref="Query{TShape}.WithMut{TComponent}"/>
    public Query<(WriteMarker<TComponent>, Nil)> WithMut<TComponent>() where TComponent : struct, IComponent => new(World, Filter);

    /// <inheritdoc cref="Query{TShape}.Has{T}"/>
    public Query Has<T>() where T : struct => new(World, Filter.Has<T>());

    /// <inheritdoc cref="Query{TShape}.Without{T}"/>
    public Query Without<T>() where T : struct => new(World, Filter.Without<T>());

    /// <inheritdoc cref="Query{TShape}.Any{T0, T1}"/>
    public Query Any<T0, T1>() where T0 : struct where T1 : struct => new(World, Filter.Any<T0, T1>());

    /// <inheritdoc cref="Query{TShape}.WithRelation{TRelation}"/>
    public Query<(RelationLinks<TRelation>, Nil)> WithRelation<TRelation>() where TRelation : struct, IRelation => With<RelationLinks<TRelation>>();

    /// <inheritdoc cref="Query{TShape}.WithoutRelation{TRelation}"/>
    public Query WithoutRelation<TRelation>() where TRelation : struct, IRelation => Without<RelationLinks<TRelation>>();
}

/// <summary>
/// A query chain's accumulated shape, from the first <c>.With</c>/<c>.WithMut</c> call
/// onward. <typeparamref name="TShape"/> is a nested 2-tuple built up one element per
/// <c>.With</c>/<c>.WithMut</c> call, terminating in <see cref="Nil"/>. A component added via
/// <c>.WithMut</c> appears wrapped in <see cref="WriteMarker{T}"/> instead of bare -- so
/// <c>.With&lt;T&gt;()</c> and <c>.WithMut&lt;T&gt;()</c> are genuinely different
/// <typeparamref name="TShape"/>s for the same <c>T</c>, not one shape reached two ways (see
/// <see cref="WriteMarker{T}"/> for why that matters). <c>.Without</c>/<c>.Has</c>/<c>.Any</c>
/// never touch it; they mutate <see cref="Filter"/> instead, so they can be applied
/// conditionally. Never used directly by hand past `world.Query()`: `.ForEach`/
/// `.ParallelForEach`/`GetEnumerator`/`TrySingle` only exist because the query-chain generator
/// emits them per shape it finds. A `.ForEach()`/`.ParallelForEach()` terminal's per-component
/// access mode (read-write vs read-only) comes from the `ref`/`in` modifier on that terminal's
/// own lambda parameter, not from `With`/`WithMut` -- those exist for `TrySingle`/a
/// `foreach`-consumed query instead, which have no lambda for the generator to read modifiers
/// from, so it reads the resolved shape's own `WriteMarker{T}`-vs-bare-`T` distinction instead.
/// </summary>
public readonly partial struct Query<TShape> : IQuery where TShape : struct
{
    /// <summary>
    /// The world this chain queries. Public, not internal, since generated code compiles
    /// into an arbitrary consumer assembly with no <c>InternalsVisibleTo</c> access to
    /// <c>Wyrd.Ecs</c>, and every generated `.ForEach`/`.ParallelForEach` extension method
    /// needs to read this field.
    /// </summary>
    public readonly World World;

    /// <summary>
    /// The filter accumulated so far via <c>.Without</c>/<c>.Has</c>/<c>.Any</c>. Public
    /// for the same reason <see cref="World"/> is: a generated `.ForEach` extension method
    /// needs to read this directly to combine it with the shape's required accessors at
    /// resolve time.
    /// </summary>
    public readonly ArchetypeQuery Filter;

    internal Query(World world) : this(world, ArchetypeQuery.Empty) { }

    internal Query(World world, ArchetypeQuery filter)
    {
        World = world;
        Filter = filter;
    }

    /// <summary>
    /// Adds <typeparamref name="TComponent"/> to the shape as read-only. For `TrySingle`/a
    /// `foreach`-consumed query, this is what makes the matching row field a `ref readonly`
    /// -- the write-vs-read distinction a `.ForEach()` terminal instead reads from its
    /// lambda's own `ref`/`in` parameter modifiers, ignoring this entirely.
    /// <typeparamref name="TComponent"/> appears bare in the shape (see
    /// <see cref="WithMut{TComponent}"/> for the wrapped alternative). <see cref="Filter"/>
    /// carries through unchanged.
    /// </summary>
    public Query<(TComponent, TShape)> With<TComponent>() where TComponent : struct, IComponent => new(World, Filter);

    /// <summary>
    /// Same as <see cref="With{TComponent}"/>, but marks <typeparamref name="TComponent"/>
    /// mutable for `TrySingle`/a `foreach`-consumed query's row field (a `.ForEach()`
    /// terminal ignores this entirely; it always reads write-vs-read from its lambda's own
    /// `ref`/`in` modifiers regardless of which of these two built the shape). Wraps
    /// <typeparamref name="TComponent"/> in <see cref="WriteMarker{T}"/> rather than placing
    /// it bare, so this and <see cref="With{TComponent}"/> are different closed
    /// <c>Query&lt;TShape&gt;</c> types for the same <typeparamref name="TComponent"/> --
    /// see <see cref="WriteMarker{T}"/> for why.
    /// </summary>
    public Query<(WriteMarker<TComponent>, TShape)> WithMut<TComponent>() where TComponent : struct, IComponent => new(World, Filter);

    /// <summary>Requires the archetype to contain <typeparamref name="T"/>, without reading its data. Does not change the shape: applies immediately to <see cref="Filter"/>, so it can be called conditionally.</summary>
    public Query<TShape> Has<T>() where T : struct => new(World, Filter.Has<T>());

    /// <summary>Requires the archetype to NOT contain <typeparamref name="T"/>. Does not change the shape: applies immediately to <see cref="Filter"/>, so it can be called conditionally.</summary>
    public Query<TShape> Without<T>() where T : struct => new(World, Filter.Without<T>());

    /// <summary>Requires the archetype to contain at least one of <typeparamref name="T0"/>/<typeparamref name="T1"/>. Does not change the shape: applies immediately to <see cref="Filter"/>, so it can be called conditionally. Calling this more than once ANDs each call's own group together.</summary>
    public Query<TShape> Any<T0, T1>() where T0 : struct where T1 : struct => new(World, Filter.Any<T0, T1>());

    /// <summary>
    /// Matches entities with at least one <typeparamref name="TRelation"/> edge, target
    /// unspecified: a wildcard presence filter, not a match against a specific target.
    /// Equivalent to <c>With&lt;RelationLinks&lt;TRelation&gt;&gt;()</c>, but reads as
    /// intent at the call site.
    /// </summary>
    public Query<(RelationLinks<TRelation>, TShape)> WithRelation<TRelation>() where TRelation : struct, IRelation => With<RelationLinks<TRelation>>();

    /// <summary>Excludes entities with any <typeparamref name="TRelation"/> edge. Equivalent to <c>Without&lt;RelationLinks&lt;TRelation&gt;&gt;()</c>, but reads as intent at the call site.</summary>
    public Query<TShape> WithoutRelation<TRelation>() where TRelation : struct, IRelation => Without<RelationLinks<TRelation>>();
}

/// <summary>Starts a query chain.</summary>
public static class WorldQueryExtensions
{
    /// <summary>Starts a query chain against <paramref name="world"/> with an empty shape and an empty filter.</summary>
    public static Query Query(this World world) => new(world);
}
