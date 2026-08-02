namespace Wyrd.Ecs.Persistence.Continuous;

/// <summary>
/// Builds a checkpoint by merging the prior one with every WAL record since it. Never
/// touches a live <see cref="World"/> or interprets a payload's bytes: every record
/// already carries an encoded value, so merging reduces to replaying records with a
/// tick in <c>(priorTick, targetTick]</c> over the prior checkpoint (keyed by entity and
/// discriminator for components, by source/target/discriminator for relations; removal
/// kinds delete rather than skip) and writing the result back via the same atomic-swap
/// <see cref="IPersistenceStore"/> path <c>World.Save</c> uses. Runs entirely out of
/// band, with no synchronization needed against a live sim thread.
/// </summary>
public static class CheckpointBuilder
{
    /// <summary>
    /// Merges <paramref name="checkpointStore"/>'s current checkpoint (if any) with
    /// every WAL record in <paramref name="walStore"/> whose tick falls in
    /// <c>(priorCheckpointTick, targetTick]</c>, and writes the result back to
    /// <paramref name="checkpointStore"/> as the new checkpoint, stamped with
    /// <paramref name="targetTick"/>.
    /// </summary>
    public static void Build(IPersistenceStore checkpointStore, IWalStore walStore, int targetTick)
    {
        var (priorTick, entries, relationEntries) = ReadCheckpoint(checkpointStore);
        var destroyed = new HashSet<EntityId>();

        foreach (var startTick in walStore.ListSegmentStartTicks())
        {
            if (startTick > targetTick) continue;

            using var segmentStream = walStore.OpenSegmentRead(startTick);
            Internal.WalSegmentIO.ReadHeader(segmentStream);

            while (Internal.WalSegmentIO.TryReadRecord(segmentStream, out var kind, out var tick, out var entityId, out var targetId, out var discriminator, out var schemaHash, out var payload))
            {
                if (tick <= priorTick || tick > targetTick) continue;
                Apply(entries, relationEntries, destroyed, kind, entityId, targetId, discriminator, schemaHash, payload);
            }
        }

        WriteCheckpoint(checkpointStore, targetTick, entries, relationEntries, destroyed);
    }

    private static void Apply(
        Dictionary<(EntityId EntityId, string Discriminator), (uint? SchemaHash, byte[] Payload)> entries,
        Dictionary<(EntityId Source, EntityId Target, string Discriminator), (uint? SchemaHash, byte[] Payload)> relationEntries,
        HashSet<EntityId> destroyed,
        WalRecordKind kind, EntityId entityId, EntityId targetId, string discriminator, uint? schemaHash, byte[] payload)
    {
        switch (kind)
        {
            case WalRecordKind.EntityCreated:
                break;

            case WalRecordKind.EntityDestroyed:
                destroyed.Add(entityId);
                break;

            case WalRecordKind.ComponentRemoved:
                entries.Remove((entityId, discriminator));
                break;

            case WalRecordKind.ComponentChanged:
            case WalRecordKind.ComponentAdded:
                entries[(entityId, discriminator)] = (schemaHash, payload);
                break;

            case WalRecordKind.RelationLinked:
                relationEntries[(entityId, targetId, discriminator)] = (schemaHash, payload);
                break;

            case WalRecordKind.RelationUnlinked:
                relationEntries.Remove((entityId, targetId, discriminator));
                break;
        }
    }

    internal static (
        int Tick,
        Dictionary<(EntityId EntityId, string Discriminator), (uint? SchemaHash, byte[] Payload)> Entries,
        Dictionary<(EntityId Source, EntityId Target, string Discriminator), (uint? SchemaHash, byte[] Payload)> RelationEntries
    ) ReadCheckpoint(IPersistenceStore checkpointStore)
    {
        Stream stream;
        try
        {
            stream = checkpointStore.OpenCheckpointRead();
        }
        catch (FileNotFoundException)
        {
            // IPersistenceStore.OpenCheckpointRead's contract: this exception (or a
            // subclass) means "no checkpoint written yet," not a real read failure.
            return (0, [], []);
        }

        using (stream)
        {
            var tick = Persistence.Internal.CheckpointRecordIO.ReadHeader(stream);
            var entries = new Dictionary<(EntityId, string), (uint?, byte[])>();
            var relationEntries = new Dictionary<(EntityId, EntityId, string), (uint?, byte[])>();
            while (Persistence.Internal.CheckpointRecordIO.TryReadRecord(stream, out var kind, out var entityId, out var targetId, out var discriminator, out var schemaHash, out var payload))
            {
                if (kind == Persistence.Internal.CheckpointRecordKind.Component)
                    entries[(entityId, discriminator)] = (schemaHash, payload);
                else
                    relationEntries[(entityId, targetId, discriminator)] = (schemaHash, payload);
            }
            return (tick, entries, relationEntries);
        }
    }

    private static void WriteCheckpoint(
        IPersistenceStore checkpointStore, int tick,
        Dictionary<(EntityId EntityId, string Discriminator), (uint? SchemaHash, byte[] Payload)> entries,
        Dictionary<(EntityId Source, EntityId Target, string Discriminator), (uint? SchemaHash, byte[] Payload)> relationEntries,
        HashSet<EntityId> destroyed)
    {
        var stream = checkpointStore.OpenCheckpointWrite();
        try
        {
            Persistence.Internal.CheckpointRecordIO.WriteHeader(stream, tick);

            foreach (var ((entityId, discriminator), (schemaHash, payload)) in entries)
            {
                if (destroyed.Contains(entityId)) continue;
                Persistence.Internal.CheckpointRecordIO.WriteRecord(stream, entityId, discriminator, schemaHash, payload);
            }

            foreach (var ((sourceId, targetId, discriminator), (schemaHash, payload)) in relationEntries)
            {
                if (destroyed.Contains(sourceId) || destroyed.Contains(targetId)) continue;
                Persistence.Internal.CheckpointRecordIO.WriteRelationRecord(stream, sourceId, targetId, discriminator, schemaHash, payload);
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
}
