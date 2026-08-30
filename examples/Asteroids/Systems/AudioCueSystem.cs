using Wyrd.Ecs.Audio;
using Wyrd.Ecs.Examples.Asteroids.Events;

namespace Wyrd.Ecs.Examples.Asteroids.Systems;

public sealed class AudioCueSystem : EcsSystem
{
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

        var audio = world.GetResource<AudioPlayer>();
        var assets = world.GetResource<GameAssets>();

        for (var i = 0; i < asteroidDestroyedCount; i++) audio.Play(assets.ExplosionSound);
        for (var i = 0; i < shipDestroyedCount; i++) audio.Play(assets.ExplosionSound);
    }
}
