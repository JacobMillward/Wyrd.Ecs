namespace Wyrd.Ecs.Persistence.Binary;

/// <summary>
/// Sets up binary (MemoryPack) save/load for a <see cref="WorldBuilder"/>. Both
/// overloads here need an explicit <see cref="CodecRegistry"/> listing which
/// component types to save. If your project also references
/// <c>Wyrd.Ecs.Persistence.Binary.Generators</c>, use the generated
/// <c>AddBinaryPersistence(store)</c>/<c>AddBinaryPersistence(path)</c> overloads
/// instead: they build that registry for you from every <c>[MemoryPackable]</c>
/// component in your project.
/// </summary>
public static class WorldBuilderBinaryPersistenceExtensions
{
    extension(WorldBuilder builder)
    {
        /// <summary>
        /// Same as
        /// <see cref="AddBinaryPersistence(WorldBuilder, IPersistenceStore, CodecRegistry)"/>
        /// but takes a file path directly, wrapping it in a <c>new FileStore(path)</c>.
        /// Use this when you just want to save to a file and don't need a custom
        /// <see cref="IPersistenceStore"/>.
        /// </summary>
        public WorldBuilder AddBinaryPersistence(string path, CodecRegistry registry) =>
            builder.AddBinaryPersistence(new FileStore(path), registry);
    }

    extension(WorldBuilder builder)
    {
        /// <summary>
        /// Sets <paramref name="store"/> as the World's default persistence store and
        /// <paramref name="registry"/> as its default component codec registry, exactly
        /// as given.
        /// </summary>
        public WorldBuilder AddBinaryPersistence(IPersistenceStore store, CodecRegistry registry) =>
            builder.SetDefaultPersistenceStore(store).SetDefaultCodecRegistry(registry);
    }
}
