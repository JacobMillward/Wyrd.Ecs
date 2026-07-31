namespace Wyrd.Ecs;

/// <summary>
/// Marker interface for relation-identity structs. Implement on a <c>struct</c> to make
/// it usable as the <c>T</c> in <see cref="RelationLinks{T}"/>/<see cref="RelationBacklinks{T}"/>
/// — the relation's own type is its identity (distinguishing <c>Likes</c> from <c>Owns</c>),
/// and the struct's own fields (if any) are the payload carried by each edge. Deliberately
/// not <see cref="IComponent"/>: a relation and a plain component are different concepts —
/// see the design's Storage model section for why folding relation identity into
/// <see cref="IComponent"/> (or leaving <c>T</c> unconstrained) was rejected. An empty
/// struct (no fields) implementing this is a marker-only relation, exactly like an empty
/// <see cref="IComponent"/> struct is legal today — there is no separate tag-relation type,
/// since a relation always occupies one component slot on its owning entity regardless of
/// whether individual edges carry data (see <see cref="RelationLinks{T}"/>'s own doc).
/// </summary>
public interface IRelation
{
}
