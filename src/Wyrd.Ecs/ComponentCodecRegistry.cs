namespace Wyrd.Ecs;

/// <summary>
/// Maps component types to a stable wire discriminator plus serialize/deserialize
/// delegates — the extension point a pluggable persistence layer is built from.
/// "Save everything" is registering every component type; a narrower policy registers
/// only the types it cares about. The same mechanism serves both, and Wyrd itself has
/// no opinion on which a given consumer chooses.
/// </summary>
public sealed class ComponentCodecRegistry
{
    private readonly Dictionary<string, IComponentCodec> _byDiscriminator = new();
    private readonly Dictionary<int, IComponentCodec> _byTypeIndex = new();
    private readonly Dictionary<(string Discriminator, uint FromSchemaHash), (uint ToSchemaHash, SchemaMigrationStep Migrate)> _migrations = new();

    /// <summary>
    /// Registers <typeparamref name="T"/> under <paramref name="discriminator"/> — a
    /// caller-chosen, stable identifier, never <see cref="Internal.TypeIndex{T}"/>.
    /// Throws if <paramref name="discriminator"/> is already registered, or if
    /// <typeparamref name="T"/> is already registered under a different discriminator
    /// (silently letting this through would leave <see cref="TryGetByTypeIndex"/> and
    /// <see cref="TryGetByDiscriminator"/> resolving to different entries for the same
    /// type — the first serializes on save, the second deserializes anything saved
    /// under the earlier discriminator, with no guarantee they agree).
    /// </summary>
    public void Register<T>(string discriminator, ComponentEncoder<T> encode, ComponentDecoder<T> decode, uint? schemaHash = null) where T : struct, IComponent
    {
        if (_byDiscriminator.ContainsKey(discriminator))
            throw new ArgumentException($"Discriminator '{discriminator}' is already registered.", nameof(discriminator));

        var typeIndex = Internal.TypeIndex<T>.Value;
        if (_byTypeIndex.TryGetValue(typeIndex, out var existing))
            throw new ArgumentException($"Type '{typeof(T)}' is already registered under discriminator '{existing.Discriminator}'.");

        var entry = new Internal.ComponentCodec<T>(discriminator, encode, decode, schemaHash);
        _byDiscriminator[discriminator] = entry;
        _byTypeIndex[typeIndex] = entry;
    }

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
    /// no schema hash to migrate toward, or if the walk reaches a hash with no
    /// registered next step (naming that specific hash, not a generic mismatch error).
    /// </summary>
    public byte[] Migrate(string discriminator, uint fromSchemaHash, byte[] bytes)
    {
        if (!TryGetByDiscriminator(discriminator, out var registered))
            throw new ArgumentException($"No registration for discriminator '{discriminator}'.", nameof(discriminator));

        if (registered.SchemaHash is not { } targetHash)
            throw new InvalidOperationException($"'{discriminator}' has no current schema hash to migrate toward.");

        var currentHash = fromSchemaHash;
        var currentBytes = bytes;
        while (currentHash != targetHash)
        {
            if (!_migrations.TryGetValue((discriminator, currentHash), out var step))
                throw new InvalidOperationException($"No migration registered for '{discriminator}' from schema hash {currentHash:X8}.");

            currentBytes = step.Migrate(currentBytes);
            currentHash = step.ToSchemaHash;
        }

        return currentBytes;
    }
}
