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
        foreach (var row in world.Query().With<Transform>().Has<Bullet>())
        {
            _bulletEntities.Add(row.Entity);
            _bulletPositions.Add(row.Transform.Position);
        }

        var hasShip = world.Query().With<Transform>().Has<Ship>().TrySingle(out var shipRow);

        // One ship, one life: guards against a single tick finding more than one asteroid
        // already overlapping the ship (e.g. two halves of a just-split pair) and queuing a
        // second DestroyEntity(ship)/ShipDestroyed for an already-dead ship.
        var shipHit = false;

        foreach (var row in world.Query().With<Transform>().With<Asteroid>())
        {
            var position = row.Transform.Position;
            var size = row.Asteroid.Size;
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
                row.DestroyEntity();
                world.Emit(new AsteroidDestroyed(size, position));
                // A bullet can only kill once: without removing it here, a still-alive bullet
                // sitting on top of a freshly-split, co-located pair could match both asteroids
                // in this same loop and double its kill count.
                _bulletEntities.RemoveAt(hitBulletIndex);
                _bulletPositions.RemoveAt(hitBulletIndex);
                continue;
            }

            if (!shipHit && hasShip && Vector3.DistanceSquared(position, shipRow.Transform.Position) <= MathF.Pow(size.Radius() + ShipRadius, 2))
            {
                shipHit = true;
                row.DestroyEntity();
                shipRow.DestroyEntity();
                world.Emit(new AsteroidDestroyed(size, position));
                world.Emit(new ShipDestroyed());
            }
        }
    }
}
