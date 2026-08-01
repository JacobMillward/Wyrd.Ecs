namespace Wyrd.Ecs;

/// <summary>
/// One change reported by a <see cref="ChangeSubscription"/>. <see cref="Related"/> is
/// only meaningful for <see cref="ChangeKind.RelationLinked"/>/<see cref="ChangeKind.RelationUnlinked"/>
/// (the edge's target) — <see cref="Entity.Null"/> for every other kind.
/// </summary>
public readonly record struct ChangeEntry(Entity Entity, Entity Related, int TypeIndex, int Tick, ChangeKind Kind);
