namespace Wyrd.Ecs;

/// <summary>
/// The change-marking bookkeeping <see cref="Mut{T}"/> needs, factored out since none of
/// it depends on the wrapped component type. Kept as its own non-generic ref struct so
/// this logic compiles to one shared routine instead of being duplicated per closed
/// <see cref="Mut{T}"/> instantiation.
/// </summary>
internal readonly ref struct ChangeMarker
{
    private readonly Span<int> _lastMarkedTick;
    private readonly int _tick;
    private readonly bool _tracked;

    internal ChangeMarker(Span<int> lastMarkedTick, int tick, bool tracked)
    {
        _lastMarkedTick = lastMarkedTick;
        _tick = tick;
        _tracked = tracked;
    }

    /// <summary>
    /// Marks the entity at <paramref name="index"/> dirty (an unconditional tick stamp:
    /// touching an entity more than once in a tick just re-stamps the same value), unless
    /// change tracking is currently off, in which case this does nothing.
    /// </summary>
    internal void Mark(int index)
    {
        if (_tracked) _lastMarkedTick[index] = _tick;
    }
}

/// <summary>
/// Tracked, mutable chunk-level access to a <typeparamref name="T"/> component.
/// Indexing marks the specific entity dirty by stamping the current tick, when this
/// component type currently has change tracking on; otherwise indexing never marks
/// anything, since nothing would ever read it. This is the tracked mutation path;
/// there is no separate untracked accessor.
/// </summary>
public readonly ref struct Mut<T> : IComponentAccessor<Mut<T>> where T : struct, IComponent
{
    private readonly Span<T> _items;
    private readonly ChangeMarker _marker;

    private Mut(Span<T> items, ChangeMarker marker)
    {
        _items = items;
        _marker = marker;
    }

    /// <inheritdoc/>
    public static int TypeIndex => Internal.TypeIndex<T>.Value;

    /// <inheritdoc/>
    public static Mut<T> CreateChunk(Array items, int[] lastMarkedTick, int tick, int start, int length, bool tracked) =>
        new(((T[])items).AsSpan(start, length), new ChangeMarker(lastMarkedTick.AsSpan(start, length), tick, tracked));

    /// <summary>The number of components accessible through this instance.</summary>
    public int Length => _items.Length;

    /// <summary>
    /// Marks the entity at <paramref name="index"/> dirty (an unconditional tick stamp:
    /// touching an entity more than once in a tick just re-stamps the same value), then
    /// returns a mutable reference to its <typeparamref name="T"/> component. Marking is
    /// skipped entirely when change tracking is currently off for this component type.
    /// </summary>
    public ref T this[int index]
    {
        get
        {
            _marker.Mark(index);
            return ref _items[index];
        }
    }
}
