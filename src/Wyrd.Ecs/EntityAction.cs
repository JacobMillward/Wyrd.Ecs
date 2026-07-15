namespace Wyrd.Ecs;

/// <summary>
/// A convenience, delegate-per-entity callback. Costs meaningfully more than
/// <see cref="ChunkAction{T0}"/> span iteration — reserved for non-hot-path code,
/// never a system that runs every tick over many entities.
/// </summary>
public delegate void EntityAction<T0>(Entity entity, ref T0 component0) where T0 : struct, IComponent;

/// <summary>Two-component overload of <see cref="EntityAction{T0}"/>.</summary>
public delegate void EntityAction<T0, T1>(Entity entity, ref T0 component0, ref T1 component1)
    where T0 : struct, IComponent
    where T1 : struct, IComponent;
