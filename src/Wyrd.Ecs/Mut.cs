namespace Wyrd.Ecs;

/// <summary>
/// Tracked, mutable chunk-level access to a <typeparamref name="T"/> component.
/// Indexing marks the specific entity dirty before returning a reference to its
/// component slot — this is the tracked mutation path; there is no separate untracked
/// accessor. Phase-4-minimal dirty tracking (a plain per-row flag) — see the design
/// spec's Dirty-tracking section for what the native-dirty-tracking phase replaces it
/// with.
/// </summary>
public readonly ref struct Mut<T> : IComponentAccessor<Mut<T>> where T : struct, IComponent
{
    private readonly Span<T> _items;
    private readonly Span<bool> _dirty;

    private Mut(Span<T> items, Span<bool> dirty)
    {
        _items = items;
        _dirty = dirty;
    }

    /// <inheritdoc/>
    public static int TypeIndex => Internal.TypeIndex<T>.Value;

    /// <inheritdoc/>
    public static Mut<T> CreateChunk(Array items, bool[] dirty, int start, int length) =>
        new(((T[])items).AsSpan(start, length), dirty.AsSpan(start, length));

    /// <summary>The number of components accessible through this instance.</summary>
    public int Length => _items.Length;

    /// <summary>
    /// Marks the entity at <paramref name="index"/> dirty, then returns a mutable
    /// reference to its <typeparamref name="T"/> component.
    /// </summary>
    public ref T this[int index]
    {
        get
        {
            _dirty[index] = true;
            return ref _items[index];
        }
    }
}
