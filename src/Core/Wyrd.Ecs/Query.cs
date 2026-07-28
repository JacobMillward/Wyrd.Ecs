namespace Wyrd.Ecs;

/// <summary>
/// A query chain's accumulated shape. <typeparamref name="TShape"/> is a nested
/// 2-tuple built up one element per <c>.With</c> call, terminating in <see cref="Nil"/>
/// — <c>.Without</c>/<c>.Has</c>/<c>.Any</c> never touch it; they mutate <see cref="Filter"/>
/// instead, which is why they can be applied conditionally (`if (x) q = q.Without&lt;T&gt;();`
/// compiles, since both branches produce the same <see cref="Query{TShape}"/> type). These
/// chain-accumulating methods are instance methods (not extension methods) deliberately:
/// each only needs to specify a component's type — e.g. `.With&lt;Position&gt;()` — and C#
/// does not allow a method call to specify only some of its type arguments explicitly and
/// infer the rest, so if `TShape` were a second method-level type parameter (as on a static
/// extension method), every call would also have to spell out `TShape` redundantly. As
/// instance methods on <see cref="Query{TShape}"/>, `TShape` is already bound by the
/// receiver, leaving only the component to specify. Never used directly by hand past
/// `world.Query()`: `.ForEach`/`.ParallelForEach` only exist because the query-chain
/// generator emits them per shape it finds — there is no generic fallback for either, since
/// C# has no variadic generics to express "one parameter per tuple element" for an arbitrary
/// <typeparamref name="TShape"/>. Each data component's access mode (read-write vs
/// read-only) is inferred from the `ref`/`in` modifier on that terminal's own parameter
/// list, not declared here.
/// </summary>
public readonly partial struct Query<TShape> : IQuery where TShape : struct
{
    /// <summary>
    /// The world this chain queries. Public, not internal — generated code compiles
    /// into an arbitrary consumer assembly with no <c>InternalsVisibleTo</c> access to
    /// <c>Wyrd.Ecs</c>, exactly the constraint <see cref="ArchetypeQuery"/>/
    /// <see cref="ArchetypeChunk"/> exist to work within — an <c>internal</c> field
    /// here would silently reintroduce that same problem for every generated
    /// `.ForEach`/`.ParallelForEach` extension method, which all need to read this
    /// field to call <see cref="ArchetypeQuery.Resolve"/>.
    /// </summary>
    public readonly World World;

    /// <summary>
    /// The filter accumulated so far via <c>.Without</c>/<c>.Has</c>/<c>.Any</c> — public
    /// for the same reason <see cref="World"/> is: a generated `.ForEach` extension method,
    /// compiled into the consumer's own assembly, needs to read this directly to combine it
    /// with the shape's statically-known required accessors at resolve time (see
    /// <see cref="ArchetypeQuery.Combine"/>).
    /// </summary>
    public readonly ArchetypeQuery Filter;

    internal Query(World world) : this(world, ArchetypeQuery.Empty) { }

    internal Query(World world, ArchetypeQuery filter)
    {
        World = world;
        Filter = filter;
    }

    /// <summary>Adds <typeparamref name="TMarker"/> — a data component — to the shape. Its Reads/Writes access mode comes from the terminal's `ref`/`in`, not from this call. <see cref="Filter"/> carries through unchanged.</summary>
    public Query<(TMarker, TShape)> With<TMarker>() where TMarker : struct => new(World, Filter);

    /// <summary>Requires the archetype to contain <typeparamref name="T"/>, without reading its data. Does not change the shape — applies immediately to <see cref="Filter"/>, so it can be called conditionally.</summary>
    public Query<TShape> Has<T>() where T : struct => new(World, Filter.Has<T>());

    /// <summary>Requires the archetype to NOT contain <typeparamref name="T"/>. Does not change the shape — applies immediately to <see cref="Filter"/>, so it can be called conditionally.</summary>
    public Query<TShape> Without<T>() where T : struct => new(World, Filter.Without<T>());

    /// <summary>Requires the archetype to contain at least one of <typeparamref name="T0"/>/<typeparamref name="T1"/>. Does not change the shape — applies immediately to <see cref="Filter"/>, so it can be called conditionally. Calling this more than once ANDs each call's own group together.</summary>
    public Query<TShape> Any<T0, T1>() where T0 : struct where T1 : struct => new(World, Filter.Any<T0, T1>());
}

/// <summary>The chain's entry point.</summary>
public static class WorldQueryExtensions
{
    /// <summary>Starts a query chain against <paramref name="world"/> with an empty shape and an empty filter.</summary>
    public static Query<Nil> Query(this World world) => new(world);
}
