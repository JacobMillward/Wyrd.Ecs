namespace Wyrd.Ecs.Persistence.Binary;

/// <summary>
/// The <c>Wyrd.Ecs.Persistence.Binary</c> package's construction-time convenience —
/// only visible once this package is installed, unlike the codec-agnostic
/// <c>WorldBuilder.SetDefaultPersistenceStore</c> in the core
/// <c>Wyrd.Ecs.Persistence</c> package. Both overloads here take an explicit
/// <see cref="ComponentCodecRegistry"/> because this library can't call
/// <c>MemoryPackAutoRegistration.RegisterAll</c> itself — that type is generated fresh
/// into whatever project references <c>Wyrd.Ecs.Persistence.Binary.Generators</c> as an
/// analyzer, scanning that project's own <c>[MemoryPackable]</c> types, and doesn't
/// exist inside this library's own compilation. A consumer with the generator wired in
/// gets a one-argument <c>AddBinaryPersistence(store)</c>/<c>AddBinaryPersistence(path)</c>
/// pair generated alongside <c>RegisterAll</c> for exactly that reason — see
/// <c>MemoryPackRegistrationGenerator</c>.
/// </summary>
public static class WorldBuilderBinaryPersistenceExtensions
{
    extension(WorldBuilder builder)
    {
        /// <summary>
        /// Shorthand for
        /// <see cref="AddBinaryPersistence(WorldBuilder, IPersistenceStore, ComponentCodecRegistry)"/>
        /// with <c>new FileStore(path)</c>.
        /// </summary>
        public WorldBuilder AddBinaryPersistence(string path, ComponentCodecRegistry registry) =>
            builder.AddBinaryPersistence(new FileStore(path), registry);
    }

    extension(WorldBuilder builder)
    {
        /// <summary>
        /// Sets <paramref name="store"/> as the World's default persistence store and
        /// <paramref name="registry"/> as its default component codec registry, exactly
        /// as given.
        /// </summary>
        public WorldBuilder AddBinaryPersistence(IPersistenceStore store, ComponentCodecRegistry registry) =>
            builder.SetDefaultPersistenceStore(store).SetDefaultComponentCodecRegistry(registry);
    }
}
