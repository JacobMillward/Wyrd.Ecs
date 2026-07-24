namespace Wyrd.Ecs;

/// <summary>
/// One component change observed by <see cref="IWorld.ReadChanges{T}"/>: the entity,
/// the tick it was last touched on, and its current value (read live at scan time —
/// there is no historical log, so an entity touched more than once since the caller's
/// watermark is reported once, with its latest value).
/// </summary>
public readonly record struct ChangedComponent<T>(Entity Entity, int Tick, T Value) where T : struct, IComponent;
