using System.Numerics;
using Wyrd.Ecs.Examples.Asteroids.Components;

namespace Wyrd.Ecs.Examples.Asteroids;

internal static class AsteroidSpawner
{
    public static void Spawn(CommandBuffer commands, EntityTemplate template, AsteroidSize size, Vector3 position, Random rng)
    {
        var angle = (float)(rng.NextDouble() * MathF.Tau);
        var speed = 40f + (float)rng.NextDouble() * 60f;
        var velocity = new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0f) * speed;
        var spin = (float)(rng.NextDouble() * 2 - 1);

        commands.CreateEntity(template)
            .AddComponent(new Transform { Position = position, Rotation = Quaternion.Identity, Scale = Vector3.One * size.Scale() })
            .AddComponent(new Velocity { Value = velocity })
            .AddComponent(new Spin { RadiansPerSecond = spin })
            .AddComponent(new Asteroid { Size = size });
    }
}
