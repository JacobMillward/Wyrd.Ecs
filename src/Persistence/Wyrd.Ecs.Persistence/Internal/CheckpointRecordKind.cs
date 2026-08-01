namespace Wyrd.Ecs.Persistence.Internal;

/// <summary>Whether a checkpoint record is a component's current value or a relation edge.</summary>
internal enum CheckpointRecordKind : byte
{
    Component = 0,
    RelationEdge = 1,
}
