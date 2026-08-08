namespace Wyrd.Ecs.Persistence.Internal;

/// <summary>Whether a checkpoint record is a component's current value, a relation edge, or a tag's presence.</summary>
internal enum CheckpointRecordKind : byte
{
    Component = 0,
    RelationEdge = 1,
    Tag = 2,
}
