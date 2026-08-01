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
public sealed class ComponentCodecRegistry
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
    /// Registers <typeparamref name="T"/> (an <see cref="IRelation"/> payload type) under
    /// <paramref name="discriminator"/>, sharing the same collision namespace
    /// <see cref="Register{T}"/> uses — a discriminator already used by a component or
    /// another relation type throws, for the same reason <see cref="Register{T}"/>
    /// itself throws on either case.
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
    /// current-process <see cref="Internal.TypeIndex{T}"/> — a different value from
    /// <see cref="TryGetRelationByTypeIndex"/>'s own <c>T</c>-keyed lookup. Used by
    /// <see cref="World.EnumerateRelations"/> while walking type-erased archetype
    /// storage, which only ever has the wrapper's own type index to look up by.
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

    /// <summary>
    /// Registers a transform from <paramref name="fromSchemaHash"/> to
    /// <paramref name="toSchemaHash"/> for <paramref name="discriminator"/> — one step
    /// in a chain, not a direct oldest-to-current transform. Throws if a step from
    /// <paramref name="fromSchemaHash"/> is already registered for this discriminator.
    /// </summary>
    public void RegisterMigration(string discriminator, uint fromSchemaHash, uint toSchemaHash, SchemaMigrationStep migrate)
    {
        var key = (discriminator, fromSchemaHash);
        if (_migrations.ContainsKey(key))
            throw new ArgumentException($"A migration from schema hash {fromSchemaHash:X8} is already registered for '{discriminator}'.");

        _migrations[key] = (toSchemaHash, migrate);
    }

    /// <summary>
    /// Walks the chain of registered migrations for <paramref name="discriminator"/>,
    /// starting at <paramref name="fromSchemaHash"/>, until reaching the discriminator's
    /// currently-registered <see cref="IComponentCodec.SchemaHash"/>. Throws if
    /// <paramref name="discriminator"/> isn't registered, if its current registration has
    /// no schema hash to migrate toward, if the walk reaches a hash with no registered
    /// next step (naming that specific hash, not a generic mismatch error), or if the
    /// walk revisits a hash it's already passed through — a misconfigured chain (e.g. a
    /// swapped <paramref name="fromSchemaHash"/>/<c>toSchemaHash</c> pair creating a
    /// cycle that never reaches the current hash) fails loudly instead of looping
    /// forever.
    /// </summary>
    public byte[] Migrate(string discriminator, uint fromSchemaHash, byte[] bytes)
    {
        if (!TryGetByDiscriminator(discriminator, out var registered))
            throw new ArgumentException($"No registration for discriminator '{discriminator}'.", nameof(discriminator));

        if (registered.SchemaHash is not { } targetHash)
            throw new InvalidOperationException($"'{discriminator}' has no current schema hash to migrate toward.");

        var currentHash = fromSchemaHash;
        var currentBytes = bytes;
        var visited = new HashSet<uint> { currentHash };
        while (currentHash != targetHash)
        {
            if (!_migrations.TryGetValue((discriminator, currentHash), out var step))
                throw new InvalidOperationException($"No migration registered for '{discriminator}' from schema hash {currentHash:X8}.");

            currentBytes = step.Migrate(currentBytes);
            currentHash = step.ToSchemaHash;

            if (!visited.Add(currentHash))
                throw new InvalidOperationException($"Migration chain for '{discriminator}' starting at schema hash {fromSchemaHash:X8} cycles back to {currentHash:X8} without ever reaching the current schema hash {targetHash:X8}.");
        }

        return currentBytes;
    }
}
