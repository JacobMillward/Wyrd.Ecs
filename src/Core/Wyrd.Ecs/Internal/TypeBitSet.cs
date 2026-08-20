namespace Wyrd.Ecs.Internal;

/// <summary>
/// A growable bitset over an arbitrary integer index space. Two independent, unrelated
/// consumers rely on it: an <see cref="Archetype"/>'s identity (indexed by
/// <see cref="TypeIndex{T}"/>'s shared component/tag numbering, process-wide and
/// permanent) and <see cref="StagePlanner"/>'s per-system read/write footprint (indexed
/// by a call-scoped <c>Dictionary&lt;Type, int&gt;</c> that's discarded the moment one
/// <c>BuildStages</c> call returns). Neither consumer's indices mean anything to the
/// other; this type only ever holds bits, and it's each caller's job to keep its own
/// index space self-consistent. The first 256 bits (<see cref="InlineWordCount"/> x 64)
/// live directly in this struct's own fields; an index at or beyond 256 falls back to a
/// heap <c>ulong[]</c> sized to fit only the overflow words needed - for the realistic
/// case (well under 256 distinct indices), every operation here is allocation-free.
/// Words beyond either operand's length are treated as zero, so
/// <see cref="Equals(TypeBitSet)"/>/<see cref="GetHashCode"/> ignore trailing zero words
/// and equal bitsets compare/hash identically regardless of construction;
/// <c>default(TypeBitSet)</c> is equivalent to <see cref="Empty"/>.
/// </summary>
internal readonly struct TypeBitSet : IEquatable<TypeBitSet>
{
    private const int InlineWordCount = 4;

    private readonly ulong _w0, _w1, _w2, _w3;
    private readonly ulong[]? _overflow;

    private TypeBitSet(ulong w0, ulong w1, ulong w2, ulong w3, ulong[]? overflow)
    {
        _w0 = w0;
        _w1 = w1;
        _w2 = w2;
        _w3 = w3;
        _overflow = overflow;
    }

    internal static readonly TypeBitSet Empty = new(0, 0, 0, 0, null);

    private int OverflowLength => _overflow?.Length ?? 0;

    private int TotalWordCount => InlineWordCount + OverflowLength;

    private ulong GetWord(int wordIndex) => wordIndex switch
    {
        0 => _w0,
        1 => _w1,
        2 => _w2,
        3 => _w3,
        _ => wordIndex - InlineWordCount < OverflowLength ? _overflow![wordIndex - InlineWordCount] : 0UL,
    };

    /// <summary>Every index set in this bitset, ascending, allocation-free.</summary>
    internal SetBitsEnumerable SetBits => new(this);

    internal readonly struct SetBitsEnumerable
    {
        private readonly TypeBitSet _bits;
        internal SetBitsEnumerable(TypeBitSet bits) => _bits = bits;
        public SetBitsEnumerator GetEnumerator() => new(_bits);
    }

    internal struct SetBitsEnumerator
    {
        private readonly TypeBitSet _bits;
        private readonly int _totalWords;
        private int _wordIndex;
        private ulong _remainingBitsInWord;
        private int _current;

        internal SetBitsEnumerator(TypeBitSet bits)
        {
            _bits = bits;
            _totalWords = bits.TotalWordCount;
            _wordIndex = 0;
            _remainingBitsInWord = _totalWords > 0 ? bits.GetWord(0) : 0UL;
            _current = -1;
        }

        public int Current => _current;

        public bool MoveNext()
        {
            while (_remainingBitsInWord == 0)
            {
                _wordIndex++;
                if (_wordIndex >= _totalWords) return false;
                _remainingBitsInWord = _bits.GetWord(_wordIndex);
            }

            var bitIndex = System.Numerics.BitOperations.TrailingZeroCount(_remainingBitsInWord);
            _current = _wordIndex * 64 + bitIndex;
            _remainingBitsInWord &= _remainingBitsInWord - 1; // clear the lowest set bit
            return true;
        }
    }

    internal bool Contains(int index)
    {
        var word = index >> 6;
        return (GetWord(word) & (1UL << (index & 63))) != 0;
    }

    internal TypeBitSet With(int index)
    {
        var word = index >> 6;
        var bit = 1UL << (index & 63);

        if (word < InlineWordCount)
        {
            return new TypeBitSet(
                word == 0 ? _w0 | bit : _w0,
                word == 1 ? _w1 | bit : _w1,
                word == 2 ? _w2 | bit : _w2,
                word == 3 ? _w3 | bit : _w3,
                _overflow);
        }

        var overflowIndex = word - InlineWordCount;
        var overflow = new ulong[Math.Max(OverflowLength, overflowIndex + 1)];
        if (_overflow is not null) Array.Copy(_overflow, overflow, _overflow.Length);
        overflow[overflowIndex] |= bit;
        return new TypeBitSet(_w0, _w1, _w2, _w3, overflow);
    }

    internal TypeBitSet Without(int index)
    {
        var word = index >> 6;
        var bit = 1UL << (index & 63);

        if (word < InlineWordCount)
        {
            return new TypeBitSet(
                word == 0 ? _w0 & ~bit : _w0,
                word == 1 ? _w1 & ~bit : _w1,
                word == 2 ? _w2 & ~bit : _w2,
                word == 3 ? _w3 & ~bit : _w3,
                _overflow);
        }

        var overflowIndex = word - InlineWordCount;
        if (overflowIndex >= OverflowLength) return this;
        var overflow = (ulong[])_overflow!.Clone();
        overflow[overflowIndex] &= ~bit;
        return new TypeBitSet(_w0, _w1, _w2, _w3, overflow);
    }

    /// <summary>True when every bit set in this bitset is also set in <paramref name="other"/>.</summary>
    internal bool IsSubsetOf(TypeBitSet other)
    {
        var length = TotalWordCount;
        for (var i = 0; i < length; i++)
        {
            var mine = GetWord(i);
            if ((mine & other.GetWord(i)) != mine) return false;
        }
        return true;
    }

    /// <summary>True when any bit set in this bitset is also set in <paramref name="other"/>.</summary>
    internal bool Intersects(TypeBitSet other)
    {
        var length = Math.Min(TotalWordCount, other.TotalWordCount);
        for (var i = 0; i < length; i++)
            if ((GetWord(i) & other.GetWord(i)) != 0)
                return true;
        return false;
    }

    /// <summary>The bitwise union of this bitset and <paramref name="other"/>: every bit set in either.</summary>
    internal TypeBitSet Union(TypeBitSet other)
    {
        var w0 = _w0 | other._w0;
        var w1 = _w1 | other._w1;
        var w2 = _w2 | other._w2;
        var w3 = _w3 | other._w3;

        var mineLength = OverflowLength;
        var theirsLength = other.OverflowLength;
        if (mineLength == 0 && theirsLength == 0) return new TypeBitSet(w0, w1, w2, w3, null);

        var overflow = new ulong[Math.Max(mineLength, theirsLength)];
        for (var i = 0; i < overflow.Length; i++)
        {
            var mine = i < mineLength ? _overflow![i] : 0UL;
            var theirs = i < theirsLength ? other._overflow![i] : 0UL;
            overflow[i] = mine | theirs;
        }
        return new TypeBitSet(w0, w1, w2, w3, overflow);
    }

    public bool Equals(TypeBitSet other)
    {
        var length = Math.Max(TotalWordCount, other.TotalWordCount);
        for (var i = 0; i < length; i++)
            if (GetWord(i) != other.GetWord(i)) return false;
        return true;
    }

    public override bool Equals(object? obj) => obj is TypeBitSet other && Equals(other);

    public override int GetHashCode()
    {
        var length = TotalWordCount;
        while (length > 0 && GetWord(length - 1) == 0) length--;

        var hash = new HashCode();
        for (var i = 0; i < length; i++)
            hash.Add(GetWord(i));
        return hash.ToHashCode();
    }
}
