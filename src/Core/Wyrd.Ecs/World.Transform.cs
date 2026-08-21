using System.Numerics;

namespace Wyrd.Ecs;

/// <summary>A composed, world-space position/rotation/scale: the result of walking an entity's <see cref="Parent"/> chain, not itself a component.</summary>
public readonly record struct WorldTransform(Vector3 Position, Quaternion Rotation, Vector3 Scale)
{
    /// <summary>Converts <paramref name="localPoint"/> (a location relative to this world transform) into world space.</summary>
    public Vector3 ToWorldPoint(Vector3 localPoint) =>
        Position + Vector3.Transform(localPoint * Scale, Rotation);

    /// <summary>Converts <paramref name="worldPoint"/> into a location relative to this world transform. Inverse of <see cref="ToWorldPoint"/>. Degenerate (produces infinity or NaN) if any axis of <see cref="Scale"/> is zero.</summary>
    public Vector3 ToLocalPoint(Vector3 worldPoint) =>
        Vector3.Transform(worldPoint - Position, Quaternion.Conjugate(Rotation)) / Scale;

    /// <summary>Converts <paramref name="localOffset"/> (a displacement, not a location, so <see cref="Position"/> plays no part) into world space.</summary>
    public Vector3 ToWorldOffset(Vector3 localOffset) => Vector3.Transform(localOffset * Scale, Rotation);

    /// <summary>Converts <paramref name="worldOffset"/> into a displacement relative to this world transform. Inverse of <see cref="ToWorldOffset"/>. Degenerate (produces infinity or NaN) if any axis of <see cref="Scale"/> is zero.</summary>
    public Vector3 ToLocalOffset(Vector3 worldOffset) => Vector3.Transform(worldOffset, Quaternion.Conjugate(Rotation)) / Scale;
}

public sealed partial class World
{
    /// <summary>
    /// <paramref name="entity"/>'s <see cref="Transform"/> composed with every ancestor's.
    /// Recurses up the <see cref="Parent"/> chain directly via <see cref="TryGetParent"/>
    /// rather than materializing it into a list first: both that lookup and
    /// <see cref="GetComponent{T}(Entity)"/> are already allocation-free (dictionary lookups
    /// against storage the archetype already owns), so this makes the whole call zero
    /// heap allocations, not just avoids one extra list. No caching across calls, so
    /// deliberately not the right choice to call many times per frame without a caller-side
    /// cache; revisit if profiling shows that matters. An entity with no
    /// <see cref="Transform"/> throws the same way <see cref="GetComponent{T}(Entity)"/> already
    /// does for a missing component. Does not guard against a <see cref="Parent"/> cycle: one
    /// recursive call per ancestor, same caller-error scenario <see cref="Ancestors"/> already
    /// documents.
    /// </summary>
    public WorldTransform GetWorldTransform(Entity entity)
    {
        var local = GetComponent<Transform>(entity);
        if (!TryGetParent(entity, out var parent))
            return new WorldTransform(local.Position, local.Rotation, local.Scale);

        return Compose(GetWorldTransform(parent), local.Position, local.Rotation, local.Scale);
    }

    /// <summary>Applies a local position/rotation/scale on top of an already-composed parent world transform.</summary>
    private static WorldTransform Compose(WorldTransform parent, Vector3 localPosition, Quaternion localRotation, Vector3 localScale) =>
        new(
            parent.Position + Vector3.Transform(localPosition * parent.Scale, parent.Rotation),
            parent.Rotation * localRotation,
            parent.Scale * localScale);

    /// <inheritdoc cref="GetWorldTransform"/>
    public WorldTransform GetInterpolatedWorldTransform(Entity entity)
    {
        var (current, previous) = ComposeInterpolated(entity);
        var alpha = (float)FixedStepAlpha;
        return new WorldTransform(
            Vector3.Lerp(previous.Position, current.Position, alpha),
            Quaternion.Slerp(previous.Rotation, current.Rotation, alpha),
            Vector3.Lerp(previous.Scale, current.Scale, alpha));
    }

    /// <summary>Composes both the current and the previous world transform in one walk of the <see cref="Parent"/> chain.</summary>
    private (WorldTransform Current, WorldTransform Previous) ComposeInterpolated(Entity entity)
    {
        var current = GetComponent<Transform>(entity);
        var currentWorld = new WorldTransform(current.Position, current.Rotation, current.Scale);

        // A static entity (no PreviousTransform) never changes, so its previous value
        // equals its current one: this link in the chain contributes no interpolation,
        // exactly, not approximately, with no separate fallback needed in the public API.
        ref var previousComponent = ref TryGetComponent<PreviousTransform>(entity, out var hasPrevious);
        var previousLocal = hasPrevious
            ? new WorldTransform(previousComponent.Position, previousComponent.Rotation, previousComponent.Scale)
            : currentWorld;

        if (!TryGetParent(entity, out var parent))
            return (currentWorld, previousLocal);

        var (parentCurrent, parentPrevious) = ComposeInterpolated(parent);
        return (
            Compose(parentCurrent, currentWorld.Position, currentWorld.Rotation, currentWorld.Scale),
            Compose(parentPrevious, previousLocal.Position, previousLocal.Rotation, previousLocal.Scale));
    }
}
