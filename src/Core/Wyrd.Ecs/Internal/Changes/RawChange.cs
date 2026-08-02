namespace Wyrd.Ecs.Internal;

/// <summary>
/// One component change, not yet encoded: the entity, the tick it was touched on, and
/// its current value, boxed. Yielded by <see cref="Internal.IComponentChangeSource.ReadRawChanges"/>,
/// feeding <see cref="Internal.ChangeFeedHub"/>'s type-erased scan path.
/// </summary>
internal readonly record struct RawChange(Entity Entity, int Tick, object Value);
