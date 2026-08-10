namespace Wyrd.Ecs.Internal;

/// <summary>
/// One-directional TypeIndex-to-display-name lookup for debug output
/// (<see cref="World.EnumerateArchetypes()"/>/<see cref="World.EnumerateEntities()"/>).
/// Never resolved name-to-type, so two types sharing a name coexist without conflict -
/// unlike a real persisted discriminator, this has no wire-format stakes. Public, despite
/// living under <c>Internal</c>: <c>Wyrd.Ecs.Generators.DebugNameGenerator</c> emits a
/// module initializer calling <see cref="Register{T}"/> directly into whatever external
/// assembly references it, so this needs to compile there too, not just within this
/// solution's own <c>InternalsVisibleTo</c> grants.
/// </summary>
public static class DebugNameRegistry
{
    private static readonly Dictionary<int, string> _byTypeIndex = new();

    /// <summary>Registers <typeparamref name="T"/>'s debug display name. Called by generated code; not meant for hand-written call sites.</summary>
    public static void Register<T>(string name) => _byTypeIndex[TypeIndex<T>.Value] = name;

    /// <summary>Looks up a registered debug display name by <see cref="Internal.TypeIndex{T}"/> value. False if nothing was ever registered for it.</summary>
    public static bool TryGetName(int typeIndex, out string name) =>
        _byTypeIndex.TryGetValue(typeIndex, out name!);
}
