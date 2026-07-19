using System.Runtime.CompilerServices;

namespace Wyrd.Ecs.Persistence;

/// <summary>
/// Extension members attaching persistence configuration to a <see cref="World"/> or
/// <see cref="WorldBuilder"/>, neither of which can gain new fields from another
/// assembly. Backed by one <see cref="ConditionalWeakTable{TKey,TValue}"/> keyed on
/// the <see cref="World"/> instance so a configured store doesn't outlive the World
/// that used it — a plain <c>Dictionary</c> would hold every World this has ever seen
/// alive for the rest of the process, which matters given how routinely Worlds are
/// created and discarded (this codebase's own test suite does so in nearly every test).
/// </summary>
public static class WorldPersistenceExtensions
{
    private static readonly ConditionalWeakTable<World, IPersistenceStore> DefaultStores = new();

    extension(World world)
    {
        /// <summary>
        /// The <see cref="IPersistenceStore"/> <see cref="WorldSnapshot.Save"/>/<see cref="WorldSnapshot.Load"/>
        /// fall back to when called without an explicit store. Null until set — either
        /// directly, or via <c>WorldBuilder.SetDefaultPersistenceStore</c>/
        /// <c>WorldBuilder.AddBinaryPersistence</c> at construction time. (Extension
        /// members can't be referenced via <c>cref</c> yet — CS1574 — so these are
        /// plain text, not links.)
        /// </summary>
        public IPersistenceStore? DefaultPersistenceStore
        {
            get => DefaultStores.TryGetValue(world, out var store) ? store : null;
            set
            {
                if (value is not null) DefaultStores.AddOrUpdate(world, value);
            }
        }
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
    }
}
