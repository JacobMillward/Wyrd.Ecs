namespace Wyrd.Ecs;

/// <summary>
/// One change reported by a <see cref="ChangeSubscription"/>. <see cref="Related"/> is
/// only meaningful for <see cref="ChangeKind.RelationLinked"/>/<see cref="ChangeKind.RelationUnlinked"/>
/// (the edge's target); it's <see cref="Entity.Null"/> for every other kind.
/// <see cref="Value"/> is only meaningful for <see cref="ChangeKind.ValueChanged"/>
/// (the tracked component's current, boxed value); it's <c>null</c> for every other kind.
/// <see cref="TypeIndex"/> is <c>null</c> for <see cref="ChangeKind.EntityCreated"/>/
/// <see cref="ChangeKind.EntityDestroyed"/>, since creation/destruction isn't associated
/// with any one component/tag/relation type.
/// </summary>
public readonly record struct ChangeEntry(Entity Entity, Entity Related, int? TypeIndex, int Tick, ChangeKind Kind, object? Value = null);
