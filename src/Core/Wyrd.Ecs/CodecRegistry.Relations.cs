namespace Wyrd.Ecs;

public sealed partial class CodecRegistry
{
    /// <summary>
    /// Registers <typeparamref name="T"/> (an <see cref="IRelation"/> payload type) under
    /// <paramref name="discriminator"/>, sharing the same collision namespace as
    /// <see cref="Register{T}"/>: a discriminator already used by a component or another
    /// relation type throws.
    /// </summary>
    public void RegisterRelation<T>(string discriminator, RelationEncoder<T> encode, RelationDecoder<T> decode, uint? schemaHash = null) where T : struct, IRelation
    {
        if (_byDiscriminator.ContainsKey(discriminator) || _relationsByDiscriminator.ContainsKey(discriminator))
            throw new ArgumentException($"Discriminator '{discriminator}' is already registered.", nameof(discriminator));

        var typeIndex = Internal.TypeIndex<T>.Value;
        if (_relationsByTypeIndex.TryGetValue(typeIndex, out var existing))
            throw new ArgumentException($"Relation type '{typeof(T)}' is already registered under discriminator '{existing.Discriminator}'.");

        var entry = new Internal.RelationCodec<T>(discriminator, encode, decode, schemaHash);
        _relationsByDiscriminator[discriminator] = entry;
        _relationsByTypeIndex[typeIndex] = entry;
        _relationsByLinksTypeIndex[Internal.TypeIndex<RelationLinks<T>>.Value] = entry;
    }

    /// <summary>Same as <see cref="TryGetByTypeIndex"/>, for a registered relation payload type.</summary>
    public bool TryGetRelationByTypeIndex(int typeIndex, out IRelationCodec registered)
    {
        if (_relationsByTypeIndex.TryGetValue(typeIndex, out var found))
        {
            registered = found;
            return true;
        }

        registered = null!;
        return false;
    }

    /// <summary>Same as <see cref="TryGetByDiscriminator"/>, for a registered relation payload type.</summary>
    public bool TryGetRelationByDiscriminator(string discriminator, out IRelationCodec registered)
    {
        if (_relationsByDiscriminator.TryGetValue(discriminator, out var found))
        {
            registered = found;
            return true;
        }

        registered = null!;
        return false;
    }

    /// <summary>
    /// Looks up a relation registration by its <c>RelationLinks{T}</c> wrapper's
    /// current-process <see cref="Internal.TypeIndex{T}"/>, different from
    /// <see cref="TryGetRelationByTypeIndex"/>'s own <c>T</c>-keyed lookup. Used by
    /// <see cref="World.EnumerateRelations"/>, which only has the wrapper's type index to
    /// look up by.
    /// </summary>
    internal bool TryGetRelationByLinksTypeIndex(int typeIndex, out IRelationCodec registered)
    {
        if (_relationsByLinksTypeIndex.TryGetValue(typeIndex, out var found))
        {
            registered = found;
            return true;
        }

        registered = null!;
        return false;
    }
}
