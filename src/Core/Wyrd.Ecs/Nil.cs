namespace Wyrd.Ecs;

/// <summary>
/// The empty query shape — <see cref="Query{TShape}"/>'s <c>TShape</c> before any
/// <c>.With</c>/<c>.Without</c>/<c>.Any</c> call. Never instantiated; read via Roslyn
/// symbols only, same role as <see cref="Writes{T}"/>/<see cref="Reads{T}"/>.
/// </summary>
public readonly struct Nil;
