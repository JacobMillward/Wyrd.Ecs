using SDL3;
using Wyrd.Ecs;
using Wyrd.Ecs.Platform;

var world = new WorldBuilder()
    .AddWindow("Asteroids", 960, 720)
    .Build();

var platform = world.GetSystem<PlatformSystem>();
while (!platform.Events.Any(e => e.Type == (uint)SDL.EventType.Quit))
    world.Update(TimeSpan.FromMilliseconds(16));
