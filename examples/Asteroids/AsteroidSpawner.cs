using System.Numerics;
using Wyrd.Ecs.Examples.Asteroids.Components;
using Wyrd.Ecs.Renderer;

namespace Wyrd.Ecs.Examples.Asteroids;

internal static class AsteroidSpawner
{
    private const int InitialWaveCount = 5;

    /// <summary>Spawns the game's opening wave: <see cref="InitialWaveCount"/> large asteroids at random positions around the playfield's edge.</summary>
    public static void SpawnInitialWave(CommandBuffer commands, EntityTemplate template, Random rng)
    {
        for (var i = 0; i < InitialWaveCount; i++)
        {
            var angle = (float)(rng.NextDouble() * MathF.Tau);
            var distance = 150f + (float)rng.NextDouble() * 200f;
            var position = new Vector3(MathF.Cos(angle) * distance, MathF.Sin(angle) * distance, 0f);
            Spawn(commands, template, AsteroidSize.Large, position, rng);
        }
    }

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
            .AddComponent(new Asteroid { Size = size })
            .AddComponent(new Sprite(SourceRect: null, Tint: size.Tint()));
    }
}
