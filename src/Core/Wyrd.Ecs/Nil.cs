namespace Wyrd.Ecs;

/// <summary>
/// The empty query shape — <see cref="Query{TShape}"/>'s <c>TShape</c> before any
/// <c>.With</c> call (<c>.Without</c>/<c>.Has</c>/<c>.Any</c> never touch <c>TShape</c> at
/// all — see <see cref="Query{TShape}.Filter"/>). Never instantiated; read via Roslyn
/// symbols only, as the tuple walk's terminator.
/// </summary>
public readonly struct Nil;
