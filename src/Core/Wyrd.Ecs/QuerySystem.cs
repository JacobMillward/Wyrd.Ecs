namespace Wyrd.Ecs;

/// <summary>
/// Sugar for the single-query, single-callback case: a subclass declares a `Build`
/// method (a static <c>Query&lt;TShape&gt;</c>-returning chain) and an `Execute`
/// method (matching that shape's <c>Writes&lt;T&gt;</c>/<c>Reads&lt;T&gt;</c> elements,
/// in canonical order), and the query-chain generator supplies
/// <see cref="EcsSystem.OnUpdate"/> — see the design's "QuerySystem: sugar for the
/// single-query, single-callback case" for the full shape and why `Build`/`Execute`
/// are name-convention-recognized rather than real C# overrides (a generic method
/// whose parameter list depends on unpacking an arbitrary `TShape` tuple isn't
/// expressible in C#, the same limitation this whole design exists to work around for
/// the fluent chain's terminals). This marker base declares no members itself —
/// <see cref="EcsSystem.OnUpdate"/> stays abstract and unimplemented here, supplied by
/// the generator's emitted `partial` class part instead. A class deriving from this
/// with no recognizable `Build`/`Execute` pair simply fails to compile with "does not
/// implement abstract member OnUpdate" — an ordinary, actionable compiler error.
/// </summary>
public abstract class QuerySystem : EcsSystem;
