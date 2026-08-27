using SDL3;
using Wyrd.Ecs.Examples.Asteroids.Components;
using Wyrd.Ecs.Examples.Asteroids.Events;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Examples.Asteroids.Systems;

public sealed class GameOverSystem : EcsSystem
{
    private static readonly TimeSpan SlowMoDuration = TimeSpan.FromSeconds(1.2);
    private static readonly ArchetypeQuery Scores = ArchetypeQuery.Empty.Access<Ref<Score>>();

    private readonly EventReader<ShipDestroyed> _shipDestroyed;
    private bool _triggered;
    private TimeSpan _slowMoRemaining;

    /// <summary>True once a ship death has started the slow-mo/game-over sequence. PauseSystem
    /// checks this so a player's own P press can never un-freeze a game-over world - only
    /// <see cref="Reset"/> (via a load) does.</summary>
    public bool HasTriggered => _triggered;

    public GameOverSystem(World world) => _shipDestroyed = world.CreateEventReader<ShipDestroyed>();

    /// <summary>Clears in-memory game-over state after a <c>World.Load()</c>: persistence never
    /// touches <see cref="World.IsPaused"/> or this system's own fields, so without this a loaded
    /// save would stay frozen under the previous run's game-over state.</summary>
    public void Reset()
    {
        _triggered = false;
        _slowMoRemaining = TimeSpan.Zero;
    }

    protected override void Execute(World world, Time time)
    {
        if (!_triggered)
        {
            if (_shipDestroyed.Read().Count == 0) return;
            _triggered = true;
            world.TimeScale = 0.25;
            _slowMoRemaining = SlowMoDuration;
            return;
        }

        if (world.IsPaused) return;

        _slowMoRemaining -= world.RealTime.Delta;
        if (_slowMoRemaining > TimeSpan.Zero) return;

        world.TimeScale = 1.0;
        world.Pause();

        var score = 0;
        foreach (var chunk in Scores.Resolve(world))
        {
            var scores = chunk.Access<Ref<Score>>();
            if (chunk.Count > 0) score = scores[0].Value;
        }
        SDL.SetWindowTitle(world.GetSystem<PlatformSystem>().Window, $"Asteroids - Game Over - Score {score} - L to reload a save");
    }
}
