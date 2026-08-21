using SDL3;
using Wyrd.Ecs;
using Wyrd.Ecs.Platform;

var world = new WorldBuilder()
    .AddWindow("aot-smoke-test", 320, 240, SDL.WindowFlags.Hidden)
    .Build();

var platform = world.GetSystem<PlatformSystem>();
if (platform.Window == IntPtr.Zero)
{
    Console.Error.WriteLine("FAIL: PlatformSystem.Window was null after construction");
    return 1;
}

var pushed = new SDL.Event { Type = (uint)SDL.EventType.Quit };
SDL.PushEvent(ref pushed);
world.Update(TimeSpan.Zero);

if (!platform.Events.Any(e => e.Type == (uint)SDL.EventType.Quit))
{
    Console.Error.WriteLine("FAIL: pushed Quit event did not appear in PlatformSystem.Events");
    return 1;
}

world.RemoveSystem(platform);

Console.WriteLine("OK: NativeAOT SDL init/window/event-pump round trip succeeded");
return 0;
