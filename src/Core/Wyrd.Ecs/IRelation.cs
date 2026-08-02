namespace Wyrd.Ecs;

/// <summary>
/// Marker interface for relation-identity structs. Implement on a <c>struct</c> to use it
/// as the <c>T</c> in <see cref="RelationLinks{T}"/>/<see cref="RelationBacklinks{T}"/>:
/// the struct's type is the relation's identity (distinguishing <c>Likes</c> from
/// <c>Owns</c>), and its fields, if any, are the payload carried by each edge. An empty
/// struct is a marker-only relation. Deliberately not <see cref="IComponent"/>, since a
/// relation and a plain component are different concepts.
/// </summary>
public interface IRelation
{
}
