namespace Wyrd.Ecs;

/// <summary>
/// Constructs a <see cref="World"/>. Exists as the entry point future construction-time
/// configuration (such as registering Systems) will extend, alongside the options
/// already here.
/// </summary>
public sealed class WorldBuilder
{
    private int _archetypeCapacity = World.DefaultArchetypeCapacity;

    /// <summary>
    /// Sets the entity capacity every archetype's dense arrays (its entity list and
    /// each component column) start at and never shrink below. The right value is
    /// specific to a game's actual entity/archetype-count distribution: too low means
    /// paying for repeated doubling growth on every archetype that ends up with more
    /// than a handful of entities; too high wastes memory on every archetype that
    /// never gets close to it, multiplied by however many distinct archetypes the game
    /// creates.
    /// </summary>
    public WorldBuilder WithArchetypeCapacity(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Archetype capacity must be positive.");

        _archetypeCapacity = capacity;
        return this;
    }

    /// <summary>
    /// Raised once, immediately after <see cref="Build"/> constructs the
    /// <see cref="World"/> — the extensibility hook a package (such as
    /// Wyrd.Ecs.Persistence) uses to associate configuration made on this builder
    /// with the resulting World, since neither WorldBuilder nor World can gain new
    /// fields from another assembly.
    /// </summary>
    public event Action<World>? OnBuilt;

    /// <summary>Builds a new <see cref="World"/> with the configured options.</summary>
    public World Build()
    {
        var world = new World(_archetypeCapacity);
        OnBuilt?.Invoke(world);
        return world;
    }
}
