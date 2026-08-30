using Wyrd.Ecs.Examples.Asteroids.Components;
using Wyrd.Ecs.Examples.Asteroids.Events;

namespace Wyrd.Ecs.Examples.Asteroids.Systems;

public sealed class SplitSystem : EcsSystem
{
    private readonly EventReader<AsteroidDestroyed> _destroyed;
    private readonly Random _rng = new();

    public SplitSystem(World world) => _destroyed = world.CreateEventReader<AsteroidDestroyed>();

    protected override void Execute(World world, Time time)
    {
        var destroyed = _destroyed.Read();
        if (destroyed.Count == 0) return;

        var assets = world.GetResource<GameAssets>();
        foreach (var e in destroyed)
        {
            if (e.Size.Smaller() is not { } smaller) continue;
            AsteroidSpawner.Spawn(world.Commands, assets.AsteroidTemplate, smaller, e.Position, _rng);
            AsteroidSpawner.Spawn(world.Commands, assets.AsteroidTemplate, smaller, e.Position, _rng);
        }
    }
}
