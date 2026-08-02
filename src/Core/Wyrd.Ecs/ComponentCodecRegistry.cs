namespace Wyrd.Ecs;

/// <summary>
/// Maps component types to a stable wire discriminator plus serialize/deserialize
/// delegates — the extension point a pluggable persistence layer is built from.
/// "Save everything" is registering every component type; a narrower policy registers
/// only the types it cares about. The same mechanism serves both, and Wyrd itself has
/// no opinion on which a given consumer chooses.
///
/// <para>
/// Also maps tag types (<see cref="ITag"/>, which carry no data and so need no codec) to
/// a display discriminator via <see cref="RegisterTag{T}"/> — used by debug/inspection
/// output (<see cref="World.EnumerateArchetypes"/>/<see cref="World.EnumerateEntities"/>),
/// not persistence. A separate dictionary pair from the component-codec ones below, not a
/// separate type, so a caller only ever threads one registry object through either use.
/// </para>
/// </summary>
public sealed partial class ComponentCodecRegistry
{
    private readonly Dictionary<string, IComponentCodec> _byDiscriminator = new();
    private readonly Dictionary<int, IComponentCodec> _byTypeIndex = new();
    private readonly Dictionary<(string Discriminator, uint FromSchemaHash), (uint ToSchemaHash, SchemaMigrationStep Migrate)> _migrations = new();
    private readonly Dictionary<string, int> _tagsByDiscriminator = new();
    private readonly Dictionary<int, string> _tagsByTypeIndex = new();
    private readonly Dictionary<string, IRelationCodec> _relationsByDiscriminator = new();
    private readonly Dictionary<int, IRelationCodec> _relationsByTypeIndex = new();
    private readonly Dictionary<int, IRelationCodec> _relationsByLinksTypeIndex = new();

    /// <summary>
    /// Registers <typeparamref name="T"/> under <paramref name="discriminator"/> — a
    /// caller-chosen, stable identifier, never <see cref="Internal.TypeIndex{T}"/>.
    /// Throws if <paramref name="discriminator"/> is already registered (by a component
    /// or a relation — see <see cref="RegisterRelation{T}"/>), or if <typeparamref name="T"/>
    /// is already registered under a different discriminator (silently letting this
    /// through would leave <see cref="TryGetByTypeIndex"/> and <see cref="TryGetByDiscriminator"/>
    /// resolving to different entries for the same type — the first serializes on save,
    /// the second deserializes anything saved under the earlier discriminator, with no
    /// guarantee they agree).
    /// </summary>
    public void Register<T>(string discriminator, ComponentEncoder<T> encode, ComponentDecoder<T> decode, uint? schemaHash = null) where T : struct, IComponent
    {
        if (_byDiscriminator.ContainsKey(discriminator) || _relationsByDiscriminator.ContainsKey(discriminator))
            throw new ArgumentException($"Discriminator '{discriminator}' is already registered.", nameof(discriminator));

        var typeIndex = Internal.TypeIndex<T>.Value;
        if (_byTypeIndex.TryGetValue(typeIndex, out var existing))
            throw new ArgumentException($"Type '{typeof(T)}' is already registered under discriminator '{existing.Discriminator}'.");

        var entry = new Internal.ComponentCodec<T>(discriminator, encode, decode, schemaHash);
        _byDiscriminator[discriminator] = entry;
        _byTypeIndex[typeIndex] = entry;
    }

    /// <summary>
    /// Every currently registered codec, in no particular order — used by a consumer
    /// that needs to act on every registered type generically, without knowing any of
    /// them by name or type (continuous persistence's change-tracking setup, for one).
    /// </summary>
    public IReadOnlyCollection<IComponentCodec> All => _byDiscriminator.Values;

    /// <summary>Looks up a registration by its current-process <see cref="Internal.TypeIndex{T}"/> — used by <see cref="World.EnumerateAll"/> while walking type-erased storage.</summary>
    public bool TryGetByTypeIndex(int typeIndex, out IComponentCodec registered)
    {
        if (_byTypeIndex.TryGetValue(typeIndex, out var found))
        {
            registered = found;
            return true;
        }

        registered = null!;
        return false;
    }

    /// <summary>Looks up a registration by its stable wire discriminator — used when deserializing saved or received data back into a <see cref="World"/>.</summary>
    public bool TryGetByDiscriminator(string discriminator, out IComponentCodec registered)
    {
        if (_byDiscriminator.TryGetValue(discriminator, out var found))
        {
            registered = found;
            return true;
        }

        registered = null!;
        return false;
    }
}
