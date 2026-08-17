using SDL3;

namespace Wyrd.Ecs.Platform.Tests;

public class PlatformSystemTests
{
    [Fact]
    public void Constructor_InitializesVideoAndCreatesAWindow()
    {
        var world = new WorldBuilder()
            .AddSystem<PlatformSystem>(w => new PlatformSystem(w, "Test Window", 320, 240, SDL.WindowFlags.Hidden))
            .Build();

        var platform = world.GetSystem<PlatformSystem>();

        platform.Window.Should().NotBe(IntPtr.Zero);
    }

    [Fact]
    public void RemoveSystem_RunsCleanupWithoutThrowing()
    {
        var world = new WorldBuilder()
            .AddSystem<PlatformSystem>(w => new PlatformSystem(w, "Test Window", 320, 240, SDL.WindowFlags.Hidden))
            .Build();
        var platform = world.GetSystem<PlatformSystem>();

        var act = () => world.RemoveSystem(platform);

        act.Should().NotThrow();
    }

    [Fact]
    public void Update_DrainsPendingEventsIntoEventsBuffer()
    {
        var world = new WorldBuilder()
            .AddSystem<PlatformSystem>(w => new PlatformSystem(w, "Test Window", 320, 240, SDL.WindowFlags.Hidden))
            .Build();
        var pushed = new SDL.Event { Type = (uint)SDL.EventType.Quit };
        SDL.PushEvent(ref pushed);

        world.Update(TimeSpan.Zero);

        var platform = world.GetSystem<PlatformSystem>();
        platform.Events.Should().Contain(e => e.Type == (uint)SDL.EventType.Quit);
    }
}
