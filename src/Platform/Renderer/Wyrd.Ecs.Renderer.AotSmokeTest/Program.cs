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

var pngPath = Path.Combine(AppContext.BaseDirectory, "smoke-test-sprite.png");
var handle = renderer.LoadTexture(pngPath);

var cameraEntity = world.Commands.CreateEntity();
world.Commands.AddComponent(cameraEntity, Transform.Identity);
world.Commands.AddComponent(cameraEntity, new Camera(0, ProjectionMode.Orthographic, true, 10f, 0.1f, 100f));

var spriteEntity = world.Commands.CreateEntity();
world.Commands.AddComponent(spriteEntity, Transform.Identity);
world.Commands.AddComponent(spriteEntity, new Sprite(SourceRect: null, Tint: Color.White));
world.Commands.AddComponent(spriteEntity, new Material(ShaderKind.UnlitSprite, handle));
world.ApplyCommands();

var loadTask = renderer.WaitForLoad(handle);
for (var i = 0; i < 50 && !loadTask.IsCompleted; i++)
{
    world.Update(TimeSpan.FromMilliseconds(16));
    Thread.Sleep(10);
}

if (!loadTask.IsCompletedSuccessfully)
{
    Console.Error.WriteLine($"FAIL: texture load did not complete successfully: {loadTask.Exception}");
    return 1;
}

for (var i = 0; i < 5; i++)
    world.Update(TimeSpan.FromMilliseconds(16));

if (renderer.FrameInFlight.CurrentFrame < 5)
{
    Console.Error.WriteLine($"FAIL: expected at least 5 frames submitted, got {renderer.FrameInFlight.CurrentFrame}");
    return 1;
}

world.RemoveSystem(renderer);
world.RemoveSystem(world.GetSystem<PlatformSystem>());

Console.WriteLine("OK: NativeAOT SDL_GPU sprite draw (device/swapchain/shader/texture/instance buffer) succeeded");
return 0;
