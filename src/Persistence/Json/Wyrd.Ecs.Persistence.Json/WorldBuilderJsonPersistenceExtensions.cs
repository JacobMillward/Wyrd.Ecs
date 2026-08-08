namespace Wyrd.Ecs.Persistence.Json;

/// <summary>
/// Sets up JSON save/load for a <see cref="WorldBuilder"/>. Both overloads here need an
/// explicit <see cref="CodecRegistry"/> listing which component types to save.
/// If your project also references <c>Wyrd.Ecs.Persistence.Json.Generators</c>, use the
/// generated <c>AddJsonPersistence(store)</c>/<c>AddJsonPersistence(path)</c> overloads
/// instead: they build that registry for you from every component type in your project.
/// </summary>
public static class WorldBuilderJsonPersistenceExtensions
{
    extension(WorldBuilder builder)
    {
        /// <summary>
        /// Same as
        /// <see cref="AddJsonPersistence(WorldBuilder, IPersistenceStore, CodecRegistry)"/>
        /// but takes a file path directly, wrapping it in a <c>new FileStore(path)</c>.
        /// Use this when you just want to save to a file and don't need a custom
        /// <see cref="IPersistenceStore"/>.
        /// </summary>
        public WorldBuilder AddJsonPersistence(string path, CodecRegistry registry) =>
            builder.AddJsonPersistence(new FileStore(path), registry);
    }

    extension(WorldBuilder builder)
    {
        /// <summary>
        /// Sets <paramref name="store"/> as the World's default persistence store and
        /// <paramref name="registry"/> as its component codec registry, exactly
        /// as given.
        /// </summary>
        public WorldBuilder AddJsonPersistence(IPersistenceStore store, CodecRegistry registry) =>
            builder.SetDefaultPersistenceStore(store).SetCodecRegistry(registry);
    }
}
