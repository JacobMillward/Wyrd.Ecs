namespace Wyrd.Ecs.Internal;

/// <summary>
/// An archetype's identity: an immutable bitset over the shared <see cref="TypeIndex{T}"/>
/// space (component and tag indices share one index space, so one bitset covers both).
/// Words beyond either operand's length are treated as zero — <see cref="Equals(ArchetypeSignature)"/>
/// and <see cref="GetHashCode"/> both ignore trailing all-zero words so equal bit sets
/// compare/hash identically regardless of how they were constructed.
/// </summary>
internal readonly struct ArchetypeSignature : IEquatable<ArchetypeSignature>
{
    private readonly ulong[] _words;

    private ArchetypeSignature(ulong[] words) => _words = words;

    internal static readonly ArchetypeSignature Empty = new(Array.Empty<ulong>());

    internal bool Contains(int typeIndex)
    {
        var word = typeIndex >> 6;
        return word < _words.Length && (_words[word] & (1UL << (typeIndex & 63))) != 0;
    }

    internal ArchetypeSignature With(int typeIndex)
    {
        var word = typeIndex >> 6;
        var words = new ulong[Math.Max(_words.Length, word + 1)];
        Array.Copy(_words, words, _words.Length);
        words[word] |= 1UL << (typeIndex & 63);
        return new ArchetypeSignature(words);
    }

    /// <summary>True when every bit set in this signature is also set in <paramref name="other"/>.</summary>
    internal bool IsSubsetOf(ArchetypeSignature other)
    {
        for (var i = 0; i < _words.Length; i++)
        {
            var theirs = i < other._words.Length ? other._words[i] : 0UL;
            if ((_words[i] & theirs) != _words[i]) return false;
        }
        return true;
    }

    /// <summary>True when any bit set in this signature is also set in <paramref name="other"/>.</summary>
    internal bool Intersects(ArchetypeSignature other)
    {
        var length = Math.Min(_words.Length, other._words.Length);
        for (var i = 0; i < length; i++)
            if ((_words[i] & other._words[i]) != 0)
                return true;
        return false;
    }

    internal ArchetypeSignature Without(int typeIndex)
    {
        var word = typeIndex >> 6;
        if (word >= _words.Length) return this;
        var words = (ulong[])_words.Clone();
        words[word] &= ~(1UL << (typeIndex & 63));
        return new ArchetypeSignature(words);
    }

    public bool Equals(ArchetypeSignature other)
    {
        var length = Math.Max(_words.Length, other._words.Length);
        for (var i = 0; i < length; i++)
        {
            var mine = i < _words.Length ? _words[i] : 0UL;
            var theirs = i < other._words.Length ? other._words[i] : 0UL;
            if (mine != theirs) return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is ArchetypeSignature other && Equals(other);

    public override int GetHashCode()
    {
        var length = _words.Length;
        while (length > 0 && _words[length - 1] == 0) length--;

        var hash = new HashCode();
        for (var i = 0; i < length; i++)
            hash.Add(_words[i]);
        return hash.ToHashCode();
    }
}
