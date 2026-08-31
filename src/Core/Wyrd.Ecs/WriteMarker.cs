namespace Wyrd.Ecs;

/// <summary>
/// A shape-only marker: <see cref="Query{TShape}.WithMut{TComponent}"/> wraps
/// <typeparamref name="T"/> in this instead of placing it bare in the shape tuple, so
/// <c>.With&lt;T&gt;()</c> and <c>.WithMut&lt;T&gt;()</c> produce genuinely different closed
/// <c>Query&lt;TShape&gt;</c> types for the same <typeparamref name="T"/> -- not the same type
/// distinguished only by which method name built it. That's what lets a `.TrySingle()`/
/// `foreach`-consumed query's generated extension methods stay unambiguous when the same
/// component is used read-only at one call site and mutably at another: there's no single
/// shared type for two colliding signatures to collide on in the first place. Never
/// instantiated; read via Roslyn symbols only, the same way <see cref="Nil"/> is.
/// </summary>
public readonly struct WriteMarker<T> where T : struct, IComponent;
