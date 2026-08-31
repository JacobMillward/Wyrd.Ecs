using Wyrd.Ecs.Examples.Asteroids.Components;
using Wyrd.Ecs.Examples.Asteroids.Events;

namespace Wyrd.Ecs.Examples.Asteroids.Systems;

public sealed partial class SplitSystem(World world) : EcsSystem
{
    [Resource] public partial GameAssets Assets { get; }

    private readonly EventReader<AsteroidDestroyed> _destroyed = world.CreateEventReader<AsteroidDestroyed>();
    private readonly Random _rng = new();

    protected override void Execute(World world, Time time)
    {
        var destroyed = _destroyed.Read();
        if (destroyed.Count == 0) return;

        foreach (var e in destroyed)
        {
            if (e.Size.Smaller() is not { } smaller) continue;
            AsteroidSpawner.Spawn(world.Commands, Assets.AsteroidTemplate, smaller, e.Position, _rng);
            AsteroidSpawner.Spawn(world.Commands, Assets.AsteroidTemplate, smaller, e.Position, _rng);
        }
    }
}
