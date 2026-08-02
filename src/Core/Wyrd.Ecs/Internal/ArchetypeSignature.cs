namespace Wyrd.Ecs.Internal;

/// <summary>
/// An archetype's identity: an immutable bitset over the shared <see cref="TypeIndex{T}"/>
/// space (components and tags share one index space). The first 256 bits
/// (<see cref="InlineWordCount"/> x 64) live directly in this struct's own fields; a type
/// index at or beyond 256 falls back to a heap <c>ulong[]</c> sized to fit only the
/// overflow words needed. Words beyond either operand's length are treated as zero, so
/// <see cref="Equals(ArchetypeSignature)"/> and <see cref="GetHashCode"/> ignore trailing
/// zero words and equal bit sets compare/hash identically regardless of construction.
/// <c>default(ArchetypeSignature)</c> is equivalent to <see cref="Empty"/>: every operation
/// treats a null <see cref="_overflow"/> the same as an empty one, so a struct-defaulted
/// value is always safe to use.
/// </summary>
internal readonly struct ArchetypeSignature : IEquatable<ArchetypeSignature>
{
    private const int InlineWordCount = 4;

    private readonly ulong _w0, _w1, _w2, _w3;
    private readonly ulong[]? _overflow;

    private ArchetypeSignature(ulong w0, ulong w1, ulong w2, ulong w3, ulong[]? overflow)
    {
        _w0 = w0;
        _w1 = w1;
        _w2 = w2;
        _w3 = w3;
        _overflow = overflow;
    }

    internal static readonly ArchetypeSignature Empty = new(0, 0, 0, 0, null);

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

    /// <summary>Every type index set in this signature, ascending, allocation-free.</summary>
    internal SetBitsEnumerable SetBits => new(this);

    internal readonly struct SetBitsEnumerable
    {
        private readonly ArchetypeSignature _signature;
        internal SetBitsEnumerable(ArchetypeSignature signature) => _signature = signature;
        public SetBitsEnumerator GetEnumerator() => new(_signature);
    }

    internal struct SetBitsEnumerator
    {
        private readonly ArchetypeSignature _signature;
        private readonly int _totalWords;
        private int _wordIndex;
        private ulong _remainingBitsInWord;
        private int _current;

        internal SetBitsEnumerator(ArchetypeSignature signature)
        {
            _signature = signature;
            _totalWords = signature.TotalWordCount;
            _wordIndex = 0;
            _remainingBitsInWord = _totalWords > 0 ? signature.GetWord(0) : 0UL;
            _current = -1;
        }

        public int Current => _current;

        public bool MoveNext()
        {
            while (_remainingBitsInWord == 0)
            {
                _wordIndex++;
                if (_wordIndex >= _totalWords) return false;
                _remainingBitsInWord = _signature.GetWord(_wordIndex);
            }

            var bitIndex = System.Numerics.BitOperations.TrailingZeroCount(_remainingBitsInWord);
            _current = _wordIndex * 64 + bitIndex;
            _remainingBitsInWord &= _remainingBitsInWord - 1; // clear the lowest set bit
            return true;
        }
    }

    internal bool Contains(int typeIndex)
    {
        var word = typeIndex >> 6;
        return (GetWord(word) & (1UL << (typeIndex & 63))) != 0;
    }

    internal ArchetypeSignature With(int typeIndex)
    {
        var word = typeIndex >> 6;
        var bit = 1UL << (typeIndex & 63);

        if (word < InlineWordCount)
        {
            return new ArchetypeSignature(
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
        return new ArchetypeSignature(_w0, _w1, _w2, _w3, overflow);
    }

    /// <summary>True when every bit set in this signature is also set in <paramref name="other"/>.</summary>
    internal bool IsSubsetOf(ArchetypeSignature other)
    {
        var length = TotalWordCount;
        for (var i = 0; i < length; i++)
        {
            var mine = GetWord(i);
            if ((mine & other.GetWord(i)) != mine) return false;
        }
        return true;
    }

    /// <summary>True when any bit set in this signature is also set in <paramref name="other"/>.</summary>
    internal bool Intersects(ArchetypeSignature other)
    {
        var length = Math.Min(TotalWordCount, other.TotalWordCount);
        for (var i = 0; i < length; i++)
            if ((GetWord(i) & other.GetWord(i)) != 0)
                return true;
        return false;
    }

    internal ArchetypeSignature Without(int typeIndex)
    {
        var word = typeIndex >> 6;
        var bit = 1UL << (typeIndex & 63);

        if (word < InlineWordCount)
        {
            return new ArchetypeSignature(
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
        return new ArchetypeSignature(_w0, _w1, _w2, _w3, overflow);
    }

    /// <summary>The bitwise union of this signature and <paramref name="other"/>: every bit set in either.</summary>
    internal ArchetypeSignature Union(ArchetypeSignature other)
    {
        var length = Math.Max(TotalWordCount, other.TotalWordCount);
        var result = this;
        for (var word = 0; word < length; word++)
        {
            var bits = other.GetWord(word) & ~GetWord(word);
            var bitIndex = 0;
            while (bits != 0)
            {
                if ((bits & 1UL) != 0) result = result.With(word * 64 + bitIndex);
                bits >>= 1;
                bitIndex++;
            }
        }
        return result;
    }

    public bool Equals(ArchetypeSignature other)
    {
        var length = Math.Max(TotalWordCount, other.TotalWordCount);
        for (var i = 0; i < length; i++)
            if (GetWord(i) != other.GetWord(i)) return false;
        return true;
    }

    public override bool Equals(object? obj) => obj is ArchetypeSignature other && Equals(other);

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
