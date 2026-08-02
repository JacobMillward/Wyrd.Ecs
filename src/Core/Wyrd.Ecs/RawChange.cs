namespace Wyrd.Ecs;

/// <summary>
/// One component change, not yet encoded: the entity, the tick it was touched on, and
/// its current value, boxed. Yielded by <see cref="Internal.IComponentChangeSource.ReadRawChanges"/> —
/// internal, feeding <see cref="Internal.ChangeFeedHub"/>'s type-erased scan path, for a
/// caller (a background persistence-capture step) that wants to scan for changes on one
/// thread and encode them on another.
/// </summary>
internal readonly record struct RawChange(Entity Entity, int Tick, object Value);
