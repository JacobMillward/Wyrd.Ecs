namespace Wyrd.Ecs.Persistence;

/// <summary>
/// The manual, on-demand save/load primitive: a full snapshot of every entity and
/// every component registered in a <see cref="SerializerRegistry"/>, written through
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
    /// </summary>
    public static void Save(World world, SerializerRegistry registry, IPersistenceStore store)
    {
        using var stream = store.OpenCheckpointWrite();

        foreach (var component in world.EnumerateAll(registry))
        {
            var entityId = world.GetPermanentId(component.Entity);
            Internal.CheckpointRecordIO.WriteRecord(stream, entityId, component.Discriminator, component.Data);
        }
    }

    /// <summary>
    /// Reads a full checkpoint from <paramref name="store"/> and reconstructs it into
    /// <paramref name="world"/>: one fresh <see cref="Entity"/> per distinct
    /// <see cref="EntityId"/> encountered, with every readable record's component
    /// added to it. A record for a discriminator absent from <paramref name="registry"/>
    /// is silently skipped, same as an unknown type is on save. A file truncated or
    /// corrupted partway through stops replay cleanly at the last complete, valid
    /// record rather than throwing or misreading garbage.
    /// </summary>
    public static void Load(World world, SerializerRegistry registry, IPersistenceStore store)
    {
        using var stream = store.OpenCheckpointRead();
        var entities = new Dictionary<EntityId, Entity>();

        while (Internal.CheckpointRecordIO.TryReadRecord(stream, out var entityId, out var discriminator, out var payload))
        {
            if (!entities.TryGetValue(entityId, out var entity))
            {
                entity = world.Commands.CreateEntity();
                entities[entityId] = entity;
            }

            if (registry.TryGetByDiscriminator(discriminator, out var registered))
                registered.DeserializeInto(world, entity, payload);
        }

        world.ApplyCommands();
    }
}
