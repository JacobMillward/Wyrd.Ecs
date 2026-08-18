using SDL3;
using Wyrd.Ecs;
using Wyrd.Ecs.Platform;
using Wyrd.Ecs.Renderer;

var world = new WorldBuilder()
    .AddPlatform("renderer-aot-smoke-test", 320, 240, SDL.WindowFlags.Hidden)
    .AddRenderer()
    .Build();

var renderer = world.GetSystem<RendererSystem>();
if (renderer.Device == IntPtr.Zero)
{
    Console.Error.WriteLine("FAIL: RendererSystem.Device was null after construction");
    return 1;
}

for (var i = 0; i < 5; i++)
    world.Update(TimeSpan.FromMilliseconds(16));

if (renderer.FrameInFlight.CurrentFrame != 5)
{
    Console.Error.WriteLine($"FAIL: expected 5 frames submitted, got {renderer.FrameInFlight.CurrentFrame}");
    return 1;
}

world.RemoveSystem(renderer);
world.RemoveSystem(world.GetSystem<PlatformSystem>());

Console.WriteLine("OK: NativeAOT SDL_GPU device/swapchain/command-buffer round trip succeeded");
return 0;
