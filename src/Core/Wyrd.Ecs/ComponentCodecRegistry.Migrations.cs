namespace Wyrd.Ecs;

public sealed partial class ComponentCodecRegistry
{
    /// <summary>
    /// Registers a transform from <paramref name="fromSchemaHash"/> to
    /// <paramref name="toSchemaHash"/> for <paramref name="discriminator"/>: one step
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
    /// <paramref name="discriminator"/> isn't registered, if its current registration has no
    /// schema hash to migrate toward, if the walk reaches a hash with no registered next
    /// step, or if the walk revisits a hash it's already passed through (a cycle in a
    /// misconfigured chain).
    /// </summary>
    public byte[] Migrate(string discriminator, uint fromSchemaHash, byte[] bytes)
    {
        uint? targetSchemaHash;
        if (TryGetByDiscriminator(discriminator, out var registered))
            targetSchemaHash = registered.SchemaHash;
        else if (TryGetRelationByDiscriminator(discriminator, out var relationRegistered))
            targetSchemaHash = relationRegistered.SchemaHash;
        else
            throw new ArgumentException($"No registration for discriminator '{discriminator}'.", nameof(discriminator));

        if (targetSchemaHash is not { } targetHash)
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
