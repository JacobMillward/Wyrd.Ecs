using System.Runtime.CompilerServices;

namespace Wyrd.Ecs;

/// <summary>
/// One matched entity's row from a <see cref="Query{T0}"/> (or a higher-arity
/// overload — this shape is reused unchanged at every arity, see the design's
/// Unified entity-tier query section). <see cref="Get{T}"/> is the single accessor
/// for every declared component type: it marks the entity dirty (the same
/// "access, not proven write" semantics <see cref="Mut{T}"/> already has) then
/// returns a mutable reference into the pre-cached, per-archetype-transition span —
/// never a fresh per-call storage lookup. See the design's Performance section.
/// </summary>
public readonly ref struct QueryRow<T0> where T0 : struct, IComponent
{
    private readonly Span<T0> _items0;
    private readonly Span<int> _lastMarkedTick0;
    private readonly DirtyLog _dirtyLog0;
    private readonly int _tick;
    private readonly int _row;
    private readonly Entity _entity;

    internal QueryRow(Span<T0> items0, Span<int> lastMarkedTick0, DirtyLog dirtyLog0, int tick, int row, Entity entity)
    {
        _items0 = items0;
        _lastMarkedTick0 = lastMarkedTick0;
        _dirtyLog0 = dirtyLog0;
        _tick = tick;
        _row = row;
        _entity = entity;
    }

    /// <summary>The entity occupying this row — free, already known by the enumerator.</summary>
    public Entity Entity => _entity;

    /// <summary>
    /// Marks the entity dirty for <typeparamref name="T"/> (deduplicated per tick),
    /// then returns a mutable reference to its <typeparamref name="T"/> component.
    /// <typeparamref name="T"/> must be one of this row's declared type arguments;
    /// see this plan's Global Constraints for why that isn't compiler-enforced.
    /// </summary>
    public ref T Get<T>() where T : struct, IComponent
    {
        if (typeof(T) == typeof(T0))
        {
            if (_lastMarkedTick0[_row] != _tick)
            {
                _lastMarkedTick0[_row] = _tick;
                _dirtyLog0.Entries[_dirtyLog0.Count] = new DirtyEntry(_entity, _tick);
                _dirtyLog0.Count++;
            }
            return ref Unsafe.As<T0, T>(ref _items0[_row]);
        }

        throw new InvalidOperationException($"Get<{typeof(T)}>() was called on a QueryRow<{typeof(T0)}> — {typeof(T)} is not one of its declared component types.");
    }
}
