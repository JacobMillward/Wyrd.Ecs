namespace Wyrd.Ecs.Internal;

/// <summary>
/// Shared amortized-growth logic for the engine's hand-rolled dense arrays (entity
/// tables, component columns and tick-stamps, archetype storage slots). Doubles on growth
/// rather than growing to the exact requested size, so repeated single-slot growth
/// stays amortized O(1) — the same rule every call site here already followed
/// independently before this existed.
/// </summary>
internal static class ArrayGrowth
{
    /// <summary>
    /// Grows <paramref name="array"/> to at least <paramref name="capacity"/>, doubling
    /// with a floor of <paramref name="minCapacity"/> so an array that starts empty
    /// doesn't get stuck doubling zero.
    /// </summary>
    internal static void EnsureCapacity<T>(ref T[] array, int capacity, int minCapacity = 4)
    {
        if (array.Length >= capacity) return;
        var newLength = Math.Max(capacity, Math.Max(array.Length * 2, minCapacity));
        Array.Resize(ref array, newLength);
    }
}
