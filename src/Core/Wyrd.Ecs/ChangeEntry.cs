namespace Wyrd.Ecs;

/// <summary>
/// One change reported by a <see cref="ChangeSubscription"/>. <see cref="Related"/> is
/// only meaningful for <see cref="ChangeKind.RelationLinked"/>/<see cref="ChangeKind.RelationUnlinked"/>
/// (the edge's target) — <see cref="Entity.Null"/> for every other kind.
/// <see cref="Value"/> is only meaningful for <see cref="ChangeKind.ValueChanged"/> —
/// the tracked component's current, boxed value — <c>null</c> for every other kind.
/// <see cref="TypeIndex"/> is <c>null</c> for <see cref="ChangeKind.EntityCreated"/>/
/// <see cref="ChangeKind.EntityDestroyed"/> — an entity's creation/destruction isn't
/// associated with any one component/tag/relation type. Nullable rather than a bare
/// <c>int</c> with an implicit "0 means none" convention: <c>Internal.TypeIndexRegistry</c>
/// assigns 0 to whichever type is indexed first, so an unreserved sentinel would collide
/// with a real, assignable type index.
/// </summary>
public readonly record struct ChangeEntry(Entity Entity, Entity Related, int? TypeIndex, int Tick, ChangeKind Kind, object? Value = null);
