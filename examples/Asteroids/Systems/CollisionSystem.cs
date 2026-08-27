using System.Numerics;
using Wyrd.Ecs.Examples.Asteroids.Components;
using Wyrd.Ecs.Examples.Asteroids.Events;

namespace Wyrd.Ecs.Examples.Asteroids.Systems;

[FixedTimestep]
[RunAfter(typeof(MovementSystem))]
public sealed class CollisionSystem : EcsSystem
{
    private const float BulletRadius = 4f;
    private const float ShipRadius = 18f;

    private static readonly ArchetypeQuery Bullets = ArchetypeQuery.Empty.Access<Ref<Transform>>().Has<Bullet>();
    private static readonly ArchetypeQuery Asteroids = ArchetypeQuery.Empty.Access<Ref<Transform>>().Access<Ref<Asteroid>>();
    private static readonly ArchetypeQuery Ships = ArchetypeQuery.Empty.Access<Ref<Transform>>().Has<Ship>();

    private readonly List<Entity> _bulletEntities = [];
    private readonly List<Vector3> _bulletPositions = [];

    protected override void Execute(World world, Time time)
    {
        _bulletEntities.Clear();
        _bulletPositions.Clear();
        foreach (var chunk in Bullets.Resolve(world))
        {
            var transforms = chunk.Access<Ref<Transform>>();
            var entities = chunk.Entities;
            for (var i = 0; i < chunk.Count; i++)
            {
                _bulletEntities.Add(entities[i]);
                _bulletPositions.Add(transforms[i].Position);
            }
        }

        Vector3? shipPosition = null;
        foreach (var chunk in Ships.Resolve(world))
        {
            var transforms = chunk.Access<Ref<Transform>>();
            if (chunk.Count > 0) shipPosition = transforms[0].Position;
        }

        foreach (var chunk in Asteroids.Resolve(world))
        {
            var transforms = chunk.Access<Ref<Transform>>();
            var asteroids = chunk.Access<Ref<Asteroid>>();
            var entities = chunk.Entities;

            for (var i = 0; i < chunk.Count; i++)
            {
                var position = transforms[i].Position;
                var size = asteroids[i].Size;
                var hitRadiusSquared = MathF.Pow(size.Radius() + BulletRadius, 2);

                var hitBulletIndex = -1;
                for (var b = 0; b < _bulletEntities.Count; b++)
                {
                    if (Vector3.DistanceSquared(position, _bulletPositions[b]) > hitRadiusSquared) continue;
                    hitBulletIndex = b;
                    break;
                }

                if (hitBulletIndex >= 0)
                {
                    world.Commands.DestroyEntity(_bulletEntities[hitBulletIndex]);
                    world.Commands.DestroyEntity(entities[i]);
                    world.Emit(new AsteroidDestroyed(size, position));
                    continue;
                }

                if (shipPosition is { } ship && Vector3.DistanceSquared(position, ship) <= MathF.Pow(size.Radius() + ShipRadius, 2))
                {
                    world.Commands.DestroyEntity(entities[i]);
                    world.Emit(new AsteroidDestroyed(size, position));
                    world.Emit(new ShipDestroyed());
                }
            }
        }
    }
}
