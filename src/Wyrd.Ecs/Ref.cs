namespace Wyrd.Ecs;

/// <summary>
/// Read-only chunk-level access to a <typeparamref name="T"/> component. Indexing
/// never marks anything dirty.
/// </summary>
public readonly ref struct Ref<T> : IComponentAccessor<Ref<T>> where T : struct, IComponent
{
    private readonly ReadOnlySpan<T> _items;

    private Ref(ReadOnlySpan<T> items) => _items = items;

    /// <inheritdoc/>
    public static int TypeIndex => Internal.TypeIndex<T>.Value;

    /// <inheritdoc/>
    public static Ref<T> CreateChunk(Array items, int[] lastMarkedTick, int tick, DirtyLog dirtyLog, int start, int length, bool tracked) =>
        new(((T[])items).AsSpan(start, length));

    /// <summary>The number of components accessible through this instance.</summary>
    public int Length => _items.Length;

    /// <summary>
    /// Returns a read-only reference to the <typeparamref name="T"/> component at
    /// <paramref name="index"/>.
    /// </summary>
    public ref readonly T this[int index] => ref _items[index];
}
