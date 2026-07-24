namespace Wyrd.Ecs.Persistence.Continuous;

/// <summary>
/// Builds a checkpoint by merging the prior one with every WAL record since it —
/// never touches a live <see cref="World"/>, never calls <c>EnumerateAll</c>, never
/// interprets a payload's bytes. Since every WAL record already durably captures a
/// component's encoded value as of some tick, "take a checkpoint" reduces to: read the
/// prior checkpoint as a baseline, apply every WAL record with a tick in
/// <c>(priorTick, targetTick]</c> in order (keyed by entity and discriminator,
/// removal-kind records deleting entries instead of being skipped), and write the
/// merged result back through the same atomic-swap <see cref="IPersistenceStore"/>
/// path <c>World.Save</c> already uses. Runs entirely out of band — no
/// synchronization with a sim thread is needed because nothing here ever touches live
/// state, only bytes already durable on disk.
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
        var (priorTick, entries) = ReadCheckpoint(checkpointStore);
        var byEntity = new Dictionary<EntityId, HashSet<string>>();
        foreach (var (entityId, discriminator) in entries.Keys)
        {
            if (!byEntity.TryGetValue(entityId, out var set))
                byEntity[entityId] = set = [];
            set.Add(discriminator);
        }

        foreach (var startTick in walStore.ListSegmentStartTicks())
        {
            if (startTick > targetTick) continue;

            using var segmentStream = walStore.OpenSegmentRead(startTick);
            Internal.WalSegmentIO.ReadHeader(segmentStream);

            while (Internal.WalSegmentIO.TryReadRecord(segmentStream, out var kind, out var tick, out var entityId, out var discriminator, out var schemaHash, out var payload))
            {
                if (tick <= priorTick || tick > targetTick) continue;
                Apply(entries, byEntity, kind, entityId, discriminator, schemaHash, payload);
            }
        }

        WriteCheckpoint(checkpointStore, targetTick, entries);
    }

    private static void Apply(
        Dictionary<(EntityId EntityId, string Discriminator), (uint? SchemaHash, byte[] Payload)> entries,
        Dictionary<EntityId, HashSet<string>> byEntity,
        WalRecordKind kind, EntityId entityId, string discriminator, uint? schemaHash, byte[] payload)
    {
        switch (kind)
        {
            case WalRecordKind.EntityCreated:
                break;

            case WalRecordKind.EntityDestroyed:
                if (byEntity.Remove(entityId, out var discriminators))
                    foreach (var d in discriminators)
                        entries.Remove((entityId, d));
                break;

            case WalRecordKind.ComponentRemoved:
                entries.Remove((entityId, discriminator));
                if (byEntity.TryGetValue(entityId, out var forRemoval))
                    forRemoval.Remove(discriminator);
                break;

            case WalRecordKind.ComponentChanged:
            case WalRecordKind.ComponentAdded:
                entries[(entityId, discriminator)] = (schemaHash, payload);
                if (!byEntity.TryGetValue(entityId, out var forEntity))
                    byEntity[entityId] = forEntity = [];
                forEntity.Add(discriminator);
                break;
        }
    }

    internal static (int Tick, Dictionary<(EntityId EntityId, string Discriminator), (uint? SchemaHash, byte[] Payload)> Entries) ReadCheckpoint(IPersistenceStore checkpointStore)
    {
        Stream stream;
        try
        {
            stream = checkpointStore.OpenCheckpointRead();
        }
        catch (FileNotFoundException)
        {
            // Required by IPersistenceStore.OpenCheckpointRead's documented contract:
            // every implementation throws exactly this (or a subclass) for "no
            // checkpoint written yet", so this catch is guaranteed to mean "empty
            // store," not a real read failure being swallowed.
            return (0, []);
        }

        using (stream)
        {
            var tick = Persistence.Internal.CheckpointRecordIO.ReadHeader(stream);
            var entries = new Dictionary<(EntityId, string), (uint?, byte[])>();
            while (Persistence.Internal.CheckpointRecordIO.TryReadRecord(stream, out var entityId, out var discriminator, out var schemaHash, out var payload))
                entries[(entityId, discriminator)] = (schemaHash, payload);
            return (tick, entries);
        }
    }

    private static void WriteCheckpoint(IPersistenceStore checkpointStore, int tick, Dictionary<(EntityId EntityId, string Discriminator), (uint? SchemaHash, byte[] Payload)> entries)
    {
        var stream = checkpointStore.OpenCheckpointWrite();
        try
        {
            Persistence.Internal.CheckpointRecordIO.WriteHeader(stream, tick);
            foreach (var ((entityId, discriminator), (schemaHash, payload)) in entries)
                Persistence.Internal.CheckpointRecordIO.WriteRecord(stream, entityId, discriminator, schemaHash, payload);
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
