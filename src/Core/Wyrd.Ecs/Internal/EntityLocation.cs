namespace Wyrd.Ecs.Internal;

/// <summary>Which archetype and row currently back a live entity.</summary>
internal readonly record struct EntityLocation(Archetype Archetype, int Row);
