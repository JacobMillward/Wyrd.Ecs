namespace Wyrd.Ecs.Internal;

/// <summary>
/// Per-relation-type traits, computed once per closed <typeparamref name="T"/> (same
/// static-generic-cache shape as <see cref="TypeIndex{T}"/>) rather than checked per call —
/// <see cref="IsExclusive"/> is read on every <see cref="CommandBuffer.AddRelation{T}(Entity, Entity, T)"/>
/// call, so the <c>typeof</c>/<see cref="Type.IsAssignableFrom"/> check that would otherwise
/// run per call instead runs once, at this type's static initialization.
/// </summary>
internal static class RelationTraits<T> where T : struct, IRelation
{
    /// <summary>True if <typeparamref name="T"/> implements <see cref="IExclusiveRelation"/> — see that interface's own doc for what this changes about <see cref="CommandBuffer.AddRelation{T}(Entity, Entity, T)"/>.</summary>
    internal static readonly bool IsExclusive = typeof(IExclusiveRelation).IsAssignableFrom(typeof(T));

    /// <summary>True if <typeparamref name="T"/> implements <see cref="IDependent"/> — see that interface's own doc for what this changes about <see cref="RelationBacklinks{T}"/>'s destroy cascade.</summary>
    internal static readonly bool IsDependent = typeof(IDependent).IsAssignableFrom(typeof(T));
}
