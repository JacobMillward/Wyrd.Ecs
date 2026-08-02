namespace Wyrd.Ecs;

/// <summary>
/// One live entity's debug/inspection view: its encoded component values and its tag
/// discriminators. Returned by <see cref="World.EnumerateEntities"/>. Unlike
/// <see cref="World.EnumerateAll"/>, every live entity gets a snapshot here, including
/// one with empty <see cref="Components"/> and <see cref="Tags"/>.
/// </summary>
public readonly record struct EntitySnapshot(
    Entity Entity,
    IReadOnlyList<EncodedComponent> Components,
    IReadOnlyList<string> Tags);
