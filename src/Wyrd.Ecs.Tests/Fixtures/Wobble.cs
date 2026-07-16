namespace Wyrd.Ecs.Tests.Fixtures;

// Deliberately defined nowhere else and registered nowhere — the point of
// ExtensibilityTests is that adding this file, with no other edits, is sufficient
// for World to store, dirty-track, and query it.
public struct Wobble : IComponent
{
    public int Intensity;
}
