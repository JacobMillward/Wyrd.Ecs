namespace Wyrd.Ecs;

/// <summary>
/// The empty query shape — <see cref="Query{TShape}"/>'s <c>TShape</c> before any
/// <c>.With</c>/<c>.Without</c>/<c>.Any</c> call. Never instantiated; read via Roslyn
/// symbols only, same role as <see cref="Has{T}"/>/<see cref="Without{T}"/>.
/// </summary>
public readonly struct Nil;
