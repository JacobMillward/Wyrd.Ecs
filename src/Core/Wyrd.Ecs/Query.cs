namespace Wyrd.Ecs;

/// <summary>
/// A query chain's accumulated shape. <typeparamref name="TShape"/> is a nested
/// 2-tuple built up one element per <c>.With</c>/<c>.Without</c>/<c>.Has</c>/<c>.Any</c>
/// call, terminating in <see cref="Nil"/>. These chain-accumulating methods are
/// instance methods (not extension methods) deliberately: each only needs to specify
/// a component's type — e.g. `.With&lt;Position&gt;()` — and C# does not
/// allow a method call to specify only some of its type arguments explicitly and
/// infer the rest, so if `TShape` were a second method-level type parameter (as on
/// a static extension method), every call would also have to spell out `TShape`
/// redundantly. As instance methods on <see cref="Query{TShape}"/>, `TShape` is
/// already bound by the receiver, leaving only the component to specify. Never used
/// directly by hand past `world.Query()`: `.ForEach`/`.ParallelForEach` only exist
/// because the query-chain generator emits them per shape it finds — there is no
/// generic fallback for either, since C# has no variadic generics to express "one
/// parameter per tuple element" for an arbitrary <typeparamref name="TShape"/>. Each
/// data component's access mode (read-write vs read-only) is inferred from the
/// `ref`/`in` modifier on that terminal's own parameter list, not declared here.
/// </summary>
public readonly struct Query<TShape> : IQueryDefinition where TShape : struct
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

    internal Query(World world) => World = world;

    /// <summary>Adds <typeparamref name="TMarker"/> — a data component — to the shape. Its Reads/Writes access mode comes from the terminal's `ref`/`in`, not from this call.</summary>
    public Query<(TMarker, TShape)> With<TMarker>() where TMarker : struct => new(World);

    /// <summary>Requires the archetype to NOT contain <typeparamref name="T"/>.</summary>
    public Query<(Without<T>, TShape)> Without<T>() where T : struct => new(World);

    /// <summary>Requires the archetype to contain <typeparamref name="T"/>, without reading its data.</summary>
    public Query<(Has<T>, TShape)> Has<T>() where T : struct => new(World);

    /// <summary>Requires the archetype to contain at least one of <typeparamref name="T0"/>/<typeparamref name="T1"/>.</summary>
    public Query<(Any<T0, T1>, TShape)> Any<T0, T1>() where T0 : struct where T1 : struct => new(World);
}

/// <summary>The chain's entry point.</summary>
public static class WorldQueryExtensions
{
    /// <summary>Starts a query chain against <paramref name="world"/> with an empty shape.</summary>
    public static Query<Nil> Query(this World world) => new(world);
}
