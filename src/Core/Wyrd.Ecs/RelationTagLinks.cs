namespace Wyrd.Ecs;

/// <summary>
/// The forward side of a tag relationship: every target entity the owning entity has a
/// <typeparamref name="T"/> edge to, with no payload. Same shape and construction
/// discipline as <see cref="RelationLinks{T}"/>, just <see cref="HashSet{T}"/>-backed
/// instead of <see cref="Dictionary{TKey,TValue}"/>-backed since there's no per-edge
/// value to store — see <see cref="RelationLinks{T}"/>'s own doc for the rest.
/// </summary>
public readonly struct RelationTagLinks<T> : IComponent where T : struct, ITag
{
    private readonly HashSet<Entity>? _targets;

    internal RelationTagLinks(HashSet<Entity> targets) => _targets = targets;

    internal HashSet<Entity>? Targets => _targets;

    /// <summary>Every target this entity has a <typeparamref name="T"/> edge to. Read-only.</summary>
    public IReadOnlyCollection<Entity> Values => _targets!;
}
