namespace Wyrd.Ecs.Persistence;

/// <summary>
/// Extension members attaching persistence configuration and manual save/load to a
/// <see cref="World"/> or <see cref="WorldBuilder"/>, neither of which can gain new fields
/// from another assembly. Backed by <see cref="Internal.WorldAttachedProperty{T}"/>, keyed
/// on the <see cref="World"/> instance so a configured store doesn't outlive the World
/// that used it.
/// </summary>
public static class WorldPersistenceExtensions
{
    private static readonly Internal.WorldAttachedProperty<IPersistenceStore> DefaultStores = new();
    private static readonly Internal.WorldAttachedProperty<ComponentCodecRegistry> DefaultRegistries = new();

    extension(World world)
    {
        /// <summary>
        /// The <see cref="IPersistenceStore"/> <c>Save</c>/<c>Load</c> fall back to when
        /// called without an explicit store. Null until set, either directly, or via
        /// <c>WorldBuilder.SetDefaultPersistenceStore</c>/<c>WorldBuilder.AddBinaryPersistence</c>
        /// at construction time. Assigning <c>null</c> clears it back to unset. (Extension
        /// members can't be referenced via <c>cref</c> yet, CS1574, so these are plain
        /// text, not links.)
        /// </summary>
        public IPersistenceStore? DefaultPersistenceStore
        {
            get => DefaultStores.Get(world);
            set => DefaultStores.Set(world, value);
        }

        /// <summary>
        /// The <see cref="ComponentCodecRegistry"/> <c>Save</c>/<c>Load</c> and a
        /// background persistence behavior (continuous persistence's capture step, for
        /// one) fall back to when they have no registry of their own to use. Null until
        /// set, either directly, or via <c>WorldBuilder.SetDefaultComponentCodecRegistry</c>
        /// at construction time. Assigning <c>null</c> clears it back to unset.
        /// </summary>
        public ComponentCodecRegistry? DefaultComponentCodecRegistry
        {
            get => DefaultRegistries.Get(world);
            set => DefaultRegistries.Set(world, value);
        }

        /// <summary>
        /// Writes a full checkpoint of every entity and every component registered in
        /// <c>World.DefaultComponentCodecRegistry</c> to <paramref name="store"/>. A
        /// component type on a live entity but absent from the registry is silently
        /// skipped, the same behavior <see cref="World.EnumerateAll"/> already has, not
        /// an error. <paramref name="store"/> defaults to <c>World.DefaultPersistenceStore</c>
        /// when omitted. If <paramref name="store"/> returns an
        /// <see cref="ITransactionalWriteStream"/> (<see cref="FileStore"/> does) and
        /// anything throws partway through the write, the stream is aborted before the
        /// exception propagates, so the previous checkpoint is never replaced by a
        /// truncated one.
        /// </summary>
        public void Save(IPersistenceStore? store = null)
        {
            store ??= ResolveDefaultStore(world);
            var registry = ResolveDefaultRegistry(world);
            var stream = store.OpenCheckpointWrite();
            try
            {
                Internal.CheckpointRecordIO.WriteHeader(stream, world.CurrentTick);

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
        /// Shorthand for <c>Save(new FileStore(path))</c>: targets a specific save file
        /// directly, for a game with more than one save slot, without touching
        /// <c>World.DefaultPersistenceStore</c>.
        /// </summary>
        public void Save(string path) => world.Save(new FileStore(path));

        /// <summary>
        /// Reads a full checkpoint from <paramref name="store"/> and reconstructs it into
        /// <paramref name="world"/>: one fresh <see cref="Entity"/> per distinct
        /// <see cref="EntityId"/> encountered, with every readable record's component
        /// added to it. A record for a discriminator absent from
        /// <c>World.DefaultComponentCodecRegistry</c> is silently skipped, same as an
        /// unknown type is on save. A file truncated or corrupted partway through stops
        /// replay cleanly at the last complete, valid record rather than throwing or
        /// misreading garbage. A record whose stored schema hash doesn't match the
        /// currently-registered type's hash is migrated via
        /// <see cref="ComponentCodecRegistry.Migrate"/> before decoding. This check is
        /// skipped entirely when either side has no schema hash registered (a record or a
        /// currently-registered type with a <c>null</c> hash). A foreign or corrupt file
        /// (bad header) throws immediately, before any record is read.
        /// <paramref name="store"/> defaults to <c>World.DefaultPersistenceStore</c>
        /// when omitted.
        /// </summary>
        public void Load(IPersistenceStore? store = null)
        {
            store ??= ResolveDefaultStore(world);
            var registry = ResolveDefaultRegistry(world);
            using var stream = store.OpenCheckpointRead();
            Internal.CheckpointRecordIO.ReadHeader(stream);
            var entities = new Dictionary<EntityId, Entity>();

            while (Internal.CheckpointRecordIO.TryReadRecord(stream, out var entityId, out var discriminator, out var schemaHash, out var payload))
            {
                if (!entities.TryGetValue(entityId, out var entity))
                {
                    entity = world.Commands.CreateEntity().Entity;
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
        /// Configures the <see cref="ComponentCodecRegistry"/> the constructed
        /// <see cref="World"/>'s <c>DefaultComponentCodecRegistry</c> is set to, applied
        /// via <see cref="WorldBuilder.OnBuilt"/> once <see cref="WorldBuilder.Build"/> runs.
        /// </summary>
        public WorldBuilder SetDefaultComponentCodecRegistry(ComponentCodecRegistry registry)
        {
            builder.OnBuilt += world => world.DefaultComponentCodecRegistry = registry;
            return builder;
        }
    }

    private static IPersistenceStore ResolveDefaultStore(World world) =>
        world.DefaultPersistenceStore
        ?? throw new InvalidOperationException(
            "No persistence store was provided and none is configured via World.DefaultPersistenceStore " +
            "(set directly, or via WorldBuilder.SetDefaultPersistenceStore/AddBinaryPersistence at construction time).");

    private static ComponentCodecRegistry ResolveDefaultRegistry(World world) =>
        world.DefaultComponentCodecRegistry
        ?? throw new InvalidOperationException(
            "No ComponentCodecRegistry was provided and none is configured via World.DefaultComponentCodecRegistry " +
            "(set directly, or via WorldBuilder.SetDefaultComponentCodecRegistry/AddBinaryPersistence/AddJsonPersistence at construction time).");
}
