namespace Wyrd.Ecs;

/// <summary>
/// Query-chain marker: this shape reads <typeparamref name="T"/>'s data (untracked,
/// `in T` access) — pass as `.With&lt;Reads&lt;T&gt;&gt;()`. Never instantiated at
/// runtime; read via Roslyn symbols by the query-chain generator. See
/// <see cref="Writes{T}"/> for why this is a new type rather than a reuse of
/// <see cref="Ref{T}"/>.
/// </summary>
public readonly struct Reads<T> where T : struct;
