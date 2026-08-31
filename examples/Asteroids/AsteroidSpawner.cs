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
        var direction = new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0f);
        var spin = (float)(rng.NextDouble() * 2 - 1);

        // SplitSystem spawns both children at their parent's exact death position. Offsetting
        // along each one's own outward direction keeps a split pair from starting exactly
        // co-located, which let a single lingering bullet clip both in the same tick and chain
        // into more splits before either had moved away.
        var spawnPosition = position + direction * size.Radius();

        commands.CreateEntity(template)
            .AddComponent(new Transform { Position = spawnPosition, Rotation = Quaternion.Identity, Scale = Vector3.One * size.Scale() })
            .AddComponent(new Velocity { Value = direction * speed })
            .AddComponent(new Spin { RadiansPerSecond = spin })
            .AddComponent(new Asteroid { Size = size })
            .AddComponent(new Sprite(SourceRect: null, Tint: size.Tint()));
    }
}
