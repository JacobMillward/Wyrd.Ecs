namespace Wyrd.Ecs;

/// <summary>
/// Query-chain marker: this shape writes <typeparamref name="T"/>'s data (tracked,
/// `ref T` access) — pass as `.With&lt;Writes&lt;T&gt;&gt;()`. Never instantiated at
/// runtime; read via Roslyn symbols by the query-chain generator. Deliberately not
/// <see cref="Mut{T}"/> — that type wraps a real <c>Span&lt;T&gt;</c> and is a
/// <c>ref struct</c> for that reason, which makes it illegal as a tuple element; this
/// type carries no runtime state at all, so it has none of that restriction.
/// </summary>
public readonly struct Writes<T> where T : struct;
