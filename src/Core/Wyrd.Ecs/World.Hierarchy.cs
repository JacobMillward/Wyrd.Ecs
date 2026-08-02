namespace Wyrd.Ecs;

public sealed partial class World
{
    /// <summary>
    /// <paramref name="child"/>'s parent, in <paramref name="parent"/>; returns whether it
    /// has one. Thin wrapper over <see cref="Targets{T}"/> for <see cref="Parent"/>,
    /// which is exclusive, so there's at most one to return.
    /// </summary>
    public bool TryGetParent(Entity child, out Entity parent)
    {
        foreach (var target in Targets<Parent>(child).Keys)
        {
            parent = target;
            return true;
        }

        parent = default;
        return false;
    }

    /// <summary><paramref name="child"/>'s parent. Throws if it has none; use <see cref="TryGetParent"/> to check first.</summary>
    public Entity GetParent(Entity child)
    {
        if (TryGetParent(child, out var parent)) return parent;
        throw new InvalidOperationException($"Entity {child} has no parent.");
    }

    /// <summary>Every direct child of <paramref name="parent"/>. Empty, not throwing, if none. Thin wrapper over <see cref="Sources{T}"/> for <see cref="Parent"/>.</summary>
    public IReadOnlyCollection<Entity> Children(Entity parent) => Sources<Parent>(parent);

    /// <summary><paramref name="entity"/>'s parent chain, closest parent first, up to (and including) the root. Does not guard against a <see cref="Parent"/> cycle: assigning a descendant as its own ancestor's parent is caller error and will loop forever, same as any other caller-error scenario this codebase doesn't defensively validate against.</summary>
    public IEnumerable<Entity> Ancestors(Entity entity)
    {
        while (TryGetParent(entity, out var parent))
        {
            yield return parent;
            entity = parent;
        }
    }

    /// <summary>Every descendant of <paramref name="entity"/>, depth-first pre-order (a node before its own children), not including <paramref name="entity"/> itself. Same cycle caveat as <see cref="Ancestors"/>.</summary>
    public IEnumerable<Entity> Descendants(Entity entity)
    {
        foreach (var child in Children(entity))
        {
            yield return child;
            foreach (var grandchild in Descendants(child))
                yield return grandchild;
        }
    }
}
