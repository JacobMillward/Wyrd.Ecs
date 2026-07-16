namespace Wyrd.Ecs.Internal;

/// <summary>
/// Binary search over a tick-ascending <see cref="DirtyEntry"/> log. Entries within one
/// storage's log are always tick-ascending — ticks only increase over a <see cref="World"/>'s
/// lifetime, and entries are appended in tick order — so an upper-bound search finds the
/// first entry after a consumer's cursor in O(log n) instead of a full linear scan.
/// </summary>
internal static class DirtyLogSearch
{
    /// <summary>The index of the first entry with <c>Tick &gt; sinceTick</c>, or <c>entries.Length</c> if none.</summary>
    internal static int FindFirstAfter(ReadOnlySpan<DirtyEntry> entries, int sinceTick)
    {
        var lo = 0;
        var hi = entries.Length;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (entries[mid].Tick <= sinceTick) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }
}
