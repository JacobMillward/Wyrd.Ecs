namespace Wyrd.Ecs;

public sealed partial class ComponentCodecRegistry
{
    /// <summary>
    /// Registers <typeparamref name="T"/> — a tag, carrying no data — under
    /// <paramref name="discriminator"/> so it can be named in debug/inspection output.
    /// Throws under the same conditions as <see cref="Register{T}"/>: a duplicate
    /// discriminator, or the same type registered twice under different discriminators.
    /// Kept as a separate dictionary pair from the component-codec ones (not merged into
    /// <see cref="IComponentCodec"/>) since a tag has no encode/decode/schema-hash to
    /// give it, and a tag discriminator is allowed to collide with a component
    /// discriminator.
    /// </summary>
    public void RegisterTag<T>(string discriminator) where T : struct, ITag
    {
        if (_tagsByDiscriminator.ContainsKey(discriminator))
            throw new ArgumentException($"Discriminator '{discriminator}' is already registered.", nameof(discriminator));

        var typeIndex = Internal.TypeIndex<T>.Value;
        if (_tagsByTypeIndex.TryGetValue(typeIndex, out var existing))
            throw new ArgumentException($"Type '{typeof(T)}' is already registered under discriminator '{existing}'.");

        _tagsByDiscriminator[discriminator] = typeIndex;
        _tagsByTypeIndex[typeIndex] = discriminator;
    }

    /// <summary>Looks up a registered tag's discriminator by its current-process <see cref="Internal.TypeIndex{T}"/>.</summary>
    public bool TryGetTagByTypeIndex(int typeIndex, out string discriminator) =>
        _tagsByTypeIndex.TryGetValue(typeIndex, out discriminator!);

    /// <summary>Looks up a registered tag's current-process <see cref="Internal.TypeIndex{T}"/> by its discriminator.</summary>
    public bool TryGetTagByDiscriminator(string discriminator, out int typeIndex) =>
        _tagsByDiscriminator.TryGetValue(discriminator, out typeIndex);
}
