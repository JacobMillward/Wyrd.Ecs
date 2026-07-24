namespace Wyrd.Ecs;

/// <summary>
/// One component change, not yet encoded: the entity, the tick it was touched on, and
/// its current value, boxed. Yielded by <see cref="IComponentCodec.ReadRawChanges"/> —
/// the deferred-encoding counterpart to <see cref="EncodedChange"/>, for a caller that
/// wants to scan for changes on one thread and encode them on another.
/// </summary>
public readonly record struct RawChange(Entity Entity, int Tick, object Value);
