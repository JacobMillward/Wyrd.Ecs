namespace Wyrd.Ecs;

/// <summary>
/// A hot-path callback invoked once per matching archetype chunk with a contiguous
/// span of <typeparamref name="T0"/> values. Obtaining this span is the tracked
/// mutation path — see the design's Dirty-tracking section; there is no separate
/// untracked accessor.
/// </summary>
public delegate void ChunkAction<T0>(Span<T0> component0) where T0 : struct, IComponent;

/// <summary>Two-component overload of <see cref="ChunkAction{T0}"/>.</summary>
public delegate void ChunkAction<T0, T1>(Span<T0> component0, Span<T1> component1)
    where T0 : struct, IComponent
    where T1 : struct, IComponent;
