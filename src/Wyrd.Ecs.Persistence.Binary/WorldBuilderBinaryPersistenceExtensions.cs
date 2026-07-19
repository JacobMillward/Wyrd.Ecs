namespace Wyrd.Ecs.Persistence.Binary;

/// <summary>
/// The <c>Wyrd.Ecs.Persistence.Binary</c> package's construction-time convenience —
/// only visible once this package is installed, unlike the codec-agnostic
/// <c>WorldBuilder.SetDefaultPersistenceStore</c> in the core
/// <c>Wyrd.Ecs.Persistence</c> package.
/// </summary>
public static class WorldBuilderBinaryPersistenceExtensions
{
    extension(WorldBuilder builder)
    {
        /// <summary>
        /// Convenience wrapper around <c>WorldBuilder.SetDefaultPersistenceStore</c>
        /// for the binary (MemoryPack) codec.
        /// </summary>
        public WorldBuilder AddBinaryPersistence(IPersistenceStore store) =>
            builder.SetDefaultPersistenceStore(store);
    }
}
