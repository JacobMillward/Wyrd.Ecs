namespace Wyrd.Ecs.Debug.Internal;

/// <summary>One live entity's debug/inspection view, mirroring <see cref="EntitySnapshot"/> but with each component's <see cref="InspectedComponent.Field"/> attached.</summary>
internal readonly record struct InspectedEntity(Entity Entity, IReadOnlyList<InspectedComponent> Components, IReadOnlyList<string> Tags);
