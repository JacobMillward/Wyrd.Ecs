namespace Wyrd.Ecs.Persistence.Continuous;

/// <summary>
/// Builds a checkpoint by merging the prior one with every WAL record since it —
/// never touches a live <see cref="World"/>, never calls <c>EnumerateAll</c>, never
/// interprets a payload's bytes. Since every WAL record already durably captures a
/// component's encoded value (or a relation edge's) as of some tick, "take a
/// checkpoint" reduces to: read the prior checkpoint as a baseline, apply every WAL
/// record with a tick in <c>(priorTick, targetTick]</c> in order (keyed by entity and
/// discriminator for components, by source/target/discriminator for relation edges;
/// removal-kind records deleting entries instead of being skipped), and write the
/// merged result back through the same atomic-swap <see cref="IPersistenceStore"/>
/// path <c>World.Save</c> already uses. Runs entirely out of band — no
/// synchronization with a sim thread is needed because nothing here ever touches live
/// state, only bytes already durable on disk.
///
/// <para>
/// A destroyed entity's entries are never removed eagerly from the working
/// dictionaries — <c>EntityDestroyed</c> only adds to a plain <c>HashSet&lt;EntityId&gt;</c>
/// (O(1), no index to maintain). The single write pass that has to run anyway (to
/// serialize the merged result) filters against that set at zero additional asymptotic
/// cost — a component entry by its owning entity, a relation entry by either its
/// source or its target — instead of an eager per-destroy sweep needing its own index
/// to stay fast. This is safe because <see cref="EntityId"/> is never reused — once an
/// id is destroyed, no later WAL record in the same merge window can legitimately
/// reference it again, so the set never needs to shrink.
/// </para>
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
            // Required by IPersistenceStore.OpenCheckpointRead's documented contract:
            // every implementation throws exactly this (or a subclass) for "no
            // checkpoint written yet", so this catch is guaranteed to mean "empty
            // store," not a real read failure being swallowed.
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
