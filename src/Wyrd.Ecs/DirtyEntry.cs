namespace Wyrd.Ecs;

/// <summary>
/// One entry from a component type's change log: the entity marked dirty, and the
/// tick it was recorded on. See the design's Streaming the change log to multiple
/// independent consumers section — entries are never removed on read, so several
/// independent cursors can each replay the same log at their own pace.
/// </summary>
public readonly record struct DirtyEntry(Entity Entity, int Tick);
