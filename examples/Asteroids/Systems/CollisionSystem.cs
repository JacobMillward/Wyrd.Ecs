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

    private readonly List<Entity> _bulletEntities = [];
    private readonly List<Vector3> _bulletPositions = [];

    protected override void Execute(World world, Time time)
    {
        _bulletEntities.Clear();
        _bulletPositions.Clear();
        world.Query().With<Transform>().Has<Bullet>().ForEach((EntityView entity, in Transform transform) =>
        {
            _bulletEntities.Add(entity.Entity);
            _bulletPositions.Add(transform.Position);
        });

        Vector3? shipPosition = null;
        world.Query().With<Transform>().Has<Ship>().ForEach((in Transform transform) => shipPosition = transform.Position);

        world.Query().With<Transform>().With<Asteroid>().ForEach((EntityView entity, in Transform transform, in Asteroid asteroid) =>
        {
            var position = transform.Position;
            var size = asteroid.Size;
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
                world.Commands.DestroyEntity(entity.Entity);
                world.Emit(new AsteroidDestroyed(size, position));
                return;
            }

            if (shipPosition is { } ship && Vector3.DistanceSquared(position, ship) <= MathF.Pow(size.Radius() + ShipRadius, 2))
            {
                world.Commands.DestroyEntity(entity.Entity);
                world.Emit(new AsteroidDestroyed(size, position));
                world.Emit(new ShipDestroyed());
            }
        });
    }
}
