namespace Wyrd.Ecs.Persistence;

/// <summary>
/// Extension members attaching persistence configuration and manual save/load to a
/// <see cref="World"/> or <see cref="WorldBuilder"/>. Backed by
/// <see cref="Internal.WorldAttachedProperty{T}"/>, keyed on the <see cref="World"/>
/// instance so a configured store doesn't outlive the World that used it.
/// </summary>
public static class WorldPersistenceExtensions
{
    private static readonly Internal.WorldAttachedProperty<IPersistenceStore> DefaultStores = new();
    private static readonly Internal.WorldAttachedProperty<CodecRegistry> Registries = new();

    extension(World world)
    {
        /// <summary>
        /// The <see cref="IPersistenceStore"/> <c>Save</c>/<c>Load</c> fall back to when
        /// called without an explicit store. Null until set, either directly or via
        /// <c>WorldBuilder.SetDefaultPersistenceStore</c>/<c>WorldBuilder.AddBinaryPersistence</c>.
        /// Assigning <c>null</c> clears it.
        /// </summary>
        public IPersistenceStore? DefaultPersistenceStore
        {
            get => DefaultStores.Get(world);
            set => DefaultStores.Set(world, value);
        }

        /// <summary>
        /// The <see cref="CodecRegistry"/> <c>Save</c>/<c>Load</c> and continuous
        /// persistence's capture step use - the World's only registry, not a fallback among
        /// alternatives (there's no per-call override the way <c>DefaultPersistenceStore</c>
        /// has one). Null until set, either directly or via
        /// <c>WorldBuilder.SetCodecRegistry</c>. Assigning <c>null</c> clears it.
        /// </summary>
        public CodecRegistry? CodecRegistry
        {
            get => Registries.Get(world);
            set => Registries.Set(world, value);
        }

        /// <summary>
        /// Writes a full checkpoint of every entity, component, relation edge, and tag
        /// registered in <c>World.CodecRegistry</c> to <paramref name="store"/>
        /// (defaults to <c>World.DefaultPersistenceStore</c>). A component or tag type
        /// present on an entity but absent from the registry is silently skipped, not an
        /// error. If the write throws partway through and <paramref name="store"/> returns
        /// an <see cref="ITransactionalWriteStream"/>, the stream is aborted first, so the
        /// previous checkpoint is never replaced by a truncated one.
        /// </summary>
        public void Save(IPersistenceStore? store = null)
        {
            store ??= ResolveDefaultStore(world);
            var registry = ResolveRegistry(world);
            var stream = store.OpenCheckpointWrite();
            try
            {
                Internal.CheckpointRecordIO.WriteHeader(stream, world.CurrentTick);

                foreach (var component in world.EnumerateAll(registry))
                {
                    var entityId = world.GetPermanentId(component.Entity);
                    Internal.CheckpointRecordIO.WriteRecord(stream, entityId, component.Discriminator, component.SchemaHash, component.Data);
                }

                foreach (var relation in world.EnumerateRelations(registry))
                {
                    var sourceId = world.GetPermanentId(relation.Source);
                    var targetId = world.GetPermanentId(relation.Target);
                    Internal.CheckpointRecordIO.WriteRelationRecord(stream, sourceId, targetId, relation.Discriminator, relation.SchemaHash, relation.Data);
                }

                foreach (var tag in world.EnumerateAllTags(registry))
                {
                    var entityId = world.GetPermanentId(tag.Entity);
                    Internal.CheckpointRecordIO.WriteTagRecord(stream, entityId, tag.Discriminator);
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
        /// Shorthand for <c>Save(new FileStore(path))</c>: targets a specific save file
        /// directly, for a game with more than one save slot, without touching
        /// <c>World.DefaultPersistenceStore</c>.
        /// </summary>
        public void Save(string path) => world.Save(new FileStore(path));

        /// <summary>
        /// Reads a full checkpoint from <paramref name="store"/> (defaults to
        /// <c>World.DefaultPersistenceStore</c>) and reconstructs it into
        /// <paramref name="world"/>. Each <see cref="EntityId"/> seen, as a component or
        /// as either side of a relation edge, gets one fresh <see cref="Entity"/> the
        /// first time it appears; an entity referenced only as a relation target is
        /// valid, not a corruption signal. A record for a discriminator absent from
        /// <c>World.CodecRegistry</c> is silently skipped, same as on
        /// save. A file truncated or corrupted mid-record stops replay cleanly at the
        /// last complete record. A record whose schema hash doesn't match the
        /// currently-registered type is migrated via
        /// <see cref="CodecRegistry.Migrate"/> first. A foreign or corrupt
        /// header throws immediately, before any record is read.
        /// </summary>
        public void Load(IPersistenceStore? store = null)
        {
            store ??= ResolveDefaultStore(world);
            var registry = ResolveRegistry(world);
            using var stream = store.OpenCheckpointRead();
            Internal.CheckpointRecordIO.ReadHeader(stream);
            var entities = new Dictionary<EntityId, Entity>();

            while (Internal.CheckpointRecordIO.TryReadRecord(stream, out var record))
            {
                if (!entities.TryGetValue(record.EntityId, out var entity))
                {
                    entity = world.Commands.CreateEntity();
                    entities[record.EntityId] = entity;
                }

                if (record.Kind == Internal.CheckpointRecordKind.Component)
                {
                    if (!registry.TryGetByDiscriminator(record.Discriminator, out var registered)) continue;

                    var bytesToDecode = record.Payload;
                    if (registered.SchemaHash is { } currentHash && record.SchemaHash is { } recordHash && recordHash != currentHash)
                        bytesToDecode = registry.Migrate(record.Discriminator, recordHash, record.Payload);

                    registered.DecodeInto(world, entity, bytesToDecode);
                }
                else if (record.Kind == Internal.CheckpointRecordKind.Tag)
                {
                    if (!registry.TryGetTagByDiscriminator(record.Discriminator, out var binder)) continue;
                    binder.Bind(world.Commands, entity);
                }
                else
                {
                    if (!entities.TryGetValue(record.TargetId, out var target))
                    {
                        target = world.Commands.CreateEntity();
                        entities[record.TargetId] = target;
                    }

                    if (!registry.TryGetRelationByDiscriminator(record.Discriminator, out var relationRegistered)) continue;

                    var bytesToDecode = record.Payload;
                    if (relationRegistered.SchemaHash is { } currentHash && record.SchemaHash is { } recordHash && recordHash != currentHash)
                        bytesToDecode = registry.Migrate(record.Discriminator, recordHash, record.Payload);

                    relationRegistered.DecodeInto(world, entity, target, bytesToDecode);
                }
            }

            world.ApplyCommands();
        }

        /// <summary>
        /// Shorthand for <c>Load(new FileStore(path))</c>: reads a specific save file
        /// directly, for a game with more than one save slot, without touching
        /// <c>World.DefaultPersistenceStore</c>.
        /// </summary>
        public void Load(string path) => world.Load(new FileStore(path));
    }

    extension(WorldBuilder builder)
    {
        /// <summary>
        /// Configures the <see cref="IPersistenceStore"/> the constructed
        /// <see cref="World"/>'s <c>DefaultPersistenceStore</c> is set to,
        /// applied via <see cref="WorldBuilder.OnBuilt"/> once <see cref="WorldBuilder.Build"/> runs.
        /// </summary>
        public WorldBuilder SetDefaultPersistenceStore(IPersistenceStore store)
        {
            builder.OnBuilt += world => world.DefaultPersistenceStore = store;
            return builder;
        }

        /// <summary>
        /// Configures the <see cref="CodecRegistry"/> the constructed
        /// <see cref="World"/>'s <c>CodecRegistry</c> is set to, applied
        /// via <see cref="WorldBuilder.OnBuilt"/> once <see cref="WorldBuilder.Build"/> runs.
        /// </summary>
        public WorldBuilder SetCodecRegistry(CodecRegistry registry)
        {
            builder.OnBuilt += world => world.CodecRegistry = registry;
            return builder;
        }
    }

    private static IPersistenceStore ResolveDefaultStore(World world) =>
        world.DefaultPersistenceStore
        ?? throw new InvalidOperationException(
            "No persistence store was provided and none is configured via World.DefaultPersistenceStore " +
            "(set directly, or via WorldBuilder.SetDefaultPersistenceStore/AddBinaryPersistence at construction time).");

    private static CodecRegistry ResolveRegistry(World world) =>
        world.CodecRegistry
        ?? throw new InvalidOperationException(
            "No CodecRegistry was provided and none is configured via World.CodecRegistry " +
            "(set directly, or via WorldBuilder.SetCodecRegistry/AddBinaryPersistence/AddJsonPersistence at construction time).");
}
