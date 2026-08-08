namespace Wyrd.Ecs.Internal;

/// <summary>
/// One-directional TypeIndex-to-display-name lookup for debug output
/// (<see cref="World.EnumerateArchetypes()"/>/<see cref="World.EnumerateEntities()"/>).
/// Never resolved name-to-type, so two types sharing a name coexist without conflict -
/// unlike a real persisted discriminator, this has no wire-format stakes.
/// </summary>
internal static class DebugNameRegistry
{
    private static readonly Dictionary<int, string> _byTypeIndex = new();

    internal static void Register<T>(string name) => _byTypeIndex[TypeIndex<T>.Value] = name;

    internal static bool TryGetName(int typeIndex, out string name) =>
        _byTypeIndex.TryGetValue(typeIndex, out name!);
}
