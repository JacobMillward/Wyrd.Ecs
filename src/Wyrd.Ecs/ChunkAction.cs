namespace Wyrd.Ecs;

/// <summary>
/// A hot-path callback invoked once per matching archetype chunk with a chunk-level
/// component accessor. <typeparamref name="TAccess0"/> is <see cref="Mut{T}"/> for
/// tracked mutable access or <see cref="Ref{T}"/> for read-only access that never
/// marks anything dirty.
/// </summary>
public delegate void ChunkAction<TAccess0>(TAccess0 component0)
    where TAccess0 : struct, IComponentAccessor<TAccess0>, allows ref struct;

/// <summary>Two-component overload of <see cref="ChunkAction{TAccess0}"/>.</summary>
public delegate void ChunkAction<TAccess0, TAccess1>(TAccess0 component0, TAccess1 component1)
    where TAccess0 : struct, IComponentAccessor<TAccess0>, allows ref struct
    where TAccess1 : struct, IComponentAccessor<TAccess1>, allows ref struct;
