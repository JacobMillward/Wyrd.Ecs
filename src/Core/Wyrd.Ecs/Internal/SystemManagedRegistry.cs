namespace Wyrd.Ecs.Internal;

/// <summary>
/// Debug names registered as system-managed (written by an engine system, not authored
/// content). Public, despite living under Internal, mirroring
/// <see cref="DebugNameRegistry"/>: <c>Wyrd.Ecs.Generators.DebugNameGenerator</c> emits
/// <see cref="Register"/> calls into whatever external assembly references it, so this
/// needs to compile there too, not just within this solution's own
/// <c>InternalsVisibleTo</c> grants.
/// </summary>
public static class SystemManagedRegistry
{
    private static readonly HashSet<string> _names = new();

    /// <summary>Registers <paramref name="debugName"/> as system-managed. Called by generated code; not meant for hand-written call sites.</summary>
    public static void Register(string debugName) => _names.Add(debugName);

    /// <summary>True if <paramref name="debugName"/> was registered as system-managed.</summary>
    public static bool IsManaged(string debugName) => _names.Contains(debugName);

    /// <summary>Removes a registration added via <see cref="Register"/>, for test cleanup only. Process-lifetime static state, no per-test scoping.</summary>
    public static void Unregister(string debugName) => _names.Remove(debugName);
}
