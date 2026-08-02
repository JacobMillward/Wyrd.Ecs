using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs;

/// <summary>
/// A stateless scan over every archetype containing <typeparamref name="T"/>, yielding
/// every row whose tick-stamp is past <c>sinceTick</c>. Obtained from
/// <see cref="World.ReadChanges{T}"/>. Any number of independent callers can scan with
/// their own watermark at any time, since reading never mutates anything, so there's
/// no coordination needed between them.
/// </summary>
internal readonly ref struct ChangedComponents<T> where T : struct, IComponent
{
    private readonly Archetype[] _archetypes;
    private readonly int _sinceTick;

    internal ChangedComponents(Archetype[] archetypes, int sinceTick)
    {
        _archetypes = archetypes;
        _sinceTick = sinceTick;
    }

    /// <summary>Returns the enumerator for this scan.</summary>
    public Enumerator GetEnumerator() => new(_archetypes, _sinceTick);

    /// <summary>Enumerates every row across every matching archetype whose tick-stamp is past the watermark.</summary>
    public ref struct Enumerator
    {
        private readonly Archetype[] _archetypes;
        private readonly int _sinceTick;
        private readonly int _typeIndex;
        private int _archetypeIndex;
        private Entity[] _entities;
        private int[] _lastMarkedTick;
        private T[] _items;
        private int _count;
        private int _row;

        internal Enumerator(Archetype[] archetypes, int sinceTick)
        {
            _archetypes = archetypes;
            _sinceTick = sinceTick;
            _typeIndex = Internal.TypeIndex<T>.Value;
            _archetypeIndex = -1;
            _entities = Array.Empty<Entity>();
            _lastMarkedTick = Array.Empty<int>();
            _items = Array.Empty<T>();
            _count = 0;
            _row = -1;
        }

        /// <summary>The current changed component.</summary>
        public ChangedComponent<T> Current => new(_entities[_row], _lastMarkedTick[_row], _items[_row]);

        /// <summary>Advances to the next row whose tick-stamp is past the watermark.</summary>
        public bool MoveNext()
        {
            _row++;
            while (true)
            {
                while (_row < _count)
                {
                    if (_lastMarkedTick[_row] > _sinceTick) return true;
                    _row++;
                }

                _archetypeIndex++;
                if (_archetypeIndex >= _archetypes.Length) return false;

                var archetype = _archetypes[_archetypeIndex];
                var storage = archetype.Storages[_typeIndex];
                _entities = archetype.Entities;
                _lastMarkedTick = storage.RawLastMarkedTick;
                _items = (T[])storage.RawItems;
                _count = archetype.Count;
                _row = 0;
            }
        }
    }
}
