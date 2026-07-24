namespace Wyrd.Ecs.Persistence.Json;

/// <summary>
/// The <c>Wyrd.Ecs.Persistence.Json</c> package's construction-time convenience —
/// only visible once this package is installed, unlike the codec-agnostic
/// <c>WorldBuilder.SetDefaultPersistenceStore</c> in the core
/// <c>Wyrd.Ecs.Persistence</c> package. Both overloads here take an explicit
/// <see cref="ComponentCodecRegistry"/> because this library can't call
/// <c>JsonAutoRegistration.RegisterAll</c> itself — that type is generated fresh into
/// whatever project references <c>Wyrd.Ecs.Persistence.Json.Generators</c>, scanning
/// that project's own <see cref="IComponent"/> types, and doesn't exist inside this
/// library's own compilation. A consumer with the generator wired in gets a
/// one-argument <c>AddJsonPersistence(store)</c>/<c>AddJsonPersistence(path)</c> pair
/// generated alongside <c>RegisterAll</c> for exactly that reason — see
/// <c>JsonRegistrationGenerator</c>.
/// </summary>
public static class WorldBuilderJsonPersistenceExtensions
{
    extension(WorldBuilder builder)
    {
        /// <summary>
        /// Shorthand for
        /// <see cref="AddJsonPersistence(WorldBuilder, IPersistenceStore, ComponentCodecRegistry)"/>
        /// with <c>new FileStore(path)</c>.
        /// </summary>
        public WorldBuilder AddJsonPersistence(string path, ComponentCodecRegistry registry) =>
            builder.AddJsonPersistence(new FileStore(path), registry);
    }

    extension(WorldBuilder builder)
    {
        /// <summary>
        /// Sets <paramref name="store"/> as the World's default persistence store and
        /// <paramref name="registry"/> as its default component codec registry, exactly
        /// as given.
        /// </summary>
        public WorldBuilder AddJsonPersistence(IPersistenceStore store, ComponentCodecRegistry registry) =>
            builder.SetDefaultPersistenceStore(store).SetDefaultComponentCodecRegistry(registry);
    }
}
