namespace Wyrd.Ecs.Internal;

/// <summary>
/// One component change observed by <see cref="World.ReadChanges{T}"/>: the entity, the
/// tick it was last touched on, and its current value, read live at scan time. Since
/// there's no historical log, an entity touched more than once since the watermark is
/// reported once, with its latest value.
/// </summary>
internal readonly record struct ChangedComponent<T>(Entity Entity, int Tick, T Value) where T : struct, IComponent;
