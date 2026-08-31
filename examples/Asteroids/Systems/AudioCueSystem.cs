using Wyrd.Ecs.Audio;
using Wyrd.Ecs.Examples.Asteroids.Events;

namespace Wyrd.Ecs.Examples.Asteroids.Systems;

public sealed partial class AudioCueSystem : EcsSystem
{
    [Resource] public partial AudioPlayer Audio { get; }
    [Resource] public partial GameAssets Assets { get; }

    private readonly EventReader<AsteroidDestroyed> _asteroidDestroyed;
    private readonly EventReader<ShipDestroyed> _shipDestroyed;

    public AudioCueSystem(World world)
    {
        _asteroidDestroyed = world.CreateEventReader<AsteroidDestroyed>();
        _shipDestroyed = world.CreateEventReader<ShipDestroyed>();
    }

    protected override void Execute(World world, Time time)
    {
        var asteroidDestroyedCount = _asteroidDestroyed.Read().Count;
        var shipDestroyedCount = _shipDestroyed.Read().Count;
        if (asteroidDestroyedCount == 0 && shipDestroyedCount == 0) return;

        for (var i = 0; i < asteroidDestroyedCount; i++) Audio.Play(Assets.ExplosionSound);
        for (var i = 0; i < shipDestroyedCount; i++) Audio.Play(Assets.ExplosionSound);
    }
}
