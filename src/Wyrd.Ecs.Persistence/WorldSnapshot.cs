namespace Wyrd.Ecs.Persistence;

/// <summary>
/// The manual, on-demand save/load primitive: a full snapshot of every entity and
/// every component registered in a <see cref="ComponentCodecRegistry"/>, written through
/// an <see cref="IPersistenceStore"/>. No background thread, no WAL — just a
/// synchronous walk of the world out, and a synchronous walk of the file back in. A
/// continuous WAL layer (a separate, later piece of this pipeline) calls
/// <see cref="Save"/> as its own periodic checkpoint rather than duplicating this walk.
/// </summary>
public static class WorldSnapshot
{
    /// <summary>
    /// Writes a full checkpoint of every entity and every component registered in
    /// <paramref name="registry"/> to <paramref name="store"/>. A component type on a
    /// live entity but absent from <paramref name="registry"/> is silently skipped —
    /// the same behavior <see cref="IWorld.EnumerateAll"/> already has, not an error.
    /// <paramref name="store"/> defaults to <paramref name="world"/>'s
    /// <c>World.DefaultPersistenceStore</c> when omitted. If <paramref name="store"/>
    /// returns an <see cref="ITransactionalWriteStream"/> (<see cref="FileStore"/>
    /// does) and anything throws partway through the write, the stream is aborted
    /// before the exception propagates, so the previous checkpoint is never replaced
    /// by a truncated one.
    /// </summary>
    public static void Save(World world, ComponentCodecRegistry registry, IPersistenceStore? store = null)
    {
        store ??= ResolveDefaultStore(world);
        var stream = store.OpenCheckpointWrite();
        try
        {
            Internal.CheckpointRecordIO.WriteHeader(stream);

            foreach (var component in world.EnumerateAll(registry))
            {
                var entityId = world.GetPermanentId(component.Entity);
                Internal.CheckpointRecordIO.WriteRecord(stream, entityId, component.Discriminator, component.SchemaHash, component.Data);
            }
        }
        catch
        {
            if (stream is ITransactionalWriteStream transactional) transactional.Abort();
            throw;
        }
        finally
        {
            stream.Dispose();
        }
    }

    /// <summary>
    /// Reads a full checkpoint from <paramref name="store"/> and reconstructs it into
    /// <paramref name="world"/>: one fresh <see cref="Entity"/> per distinct
    /// <see cref="EntityId"/> encountered, with every readable record's component
    /// added to it. A record for a discriminator absent from <paramref name="registry"/>
    /// is silently skipped, same as an unknown type is on save. A file truncated or
    /// corrupted partway through stops replay cleanly at the last complete, valid
    /// record rather than throwing or misreading garbage. A record whose stored schema
    /// hash doesn't match the currently-registered type's hash is migrated via
    /// <see cref="ComponentCodecRegistry.Migrate"/> before decoding — this check is
    /// skipped entirely when either side has no schema hash registered (a record or a
    /// currently-registered type with a <c>null</c> hash). A foreign or corrupt file
    /// (bad header) throws immediately, before any record is read.
    /// <paramref name="store"/> defaults to <paramref name="world"/>'s
    /// <c>World.DefaultPersistenceStore</c> when omitted.
    /// </summary>
    public static void Load(World world, ComponentCodecRegistry registry, IPersistenceStore? store = null)
    {
        store ??= ResolveDefaultStore(world);
        using var stream = store.OpenCheckpointRead();
        Internal.CheckpointRecordIO.ReadHeader(stream);
        var entities = new Dictionary<EntityId, Entity>();

        while (Internal.CheckpointRecordIO.TryReadRecord(stream, out var entityId, out var discriminator, out var schemaHash, out var payload))
        {
            if (!entities.TryGetValue(entityId, out var entity))
            {
                entity = world.Commands.CreateEntity();
                entities[entityId] = entity;
            }

            if (!registry.TryGetByDiscriminator(discriminator, out var registered)) continue;

            var bytesToDecode = payload;
            if (registered.SchemaHash is { } currentHash && schemaHash is { } recordHash && recordHash != currentHash)
                bytesToDecode = registry.Migrate(discriminator, recordHash, payload);

            registered.DecodeInto(world, entity, bytesToDecode);
        }

        world.ApplyCommands();
    }

    private static IPersistenceStore ResolveDefaultStore(World world) =>
        world.DefaultPersistenceStore
        ?? throw new InvalidOperationException(
            "No persistence store was provided and none is configured via World.DefaultPersistenceStore " +
            "(set directly, or via WorldBuilder.SetDefaultPersistenceStore/AddBinaryPersistence at construction time).");
}
