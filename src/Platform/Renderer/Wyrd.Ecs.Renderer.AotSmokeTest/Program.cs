using System.Numerics;
using SDL3;
using Wyrd.Ecs;
using Wyrd.Ecs.Platform;
using Wyrd.Ecs.Renderer;

var world = new WorldBuilder()
    .AddPlatform("renderer-aot-smoke-test", 320, 240, SDL.WindowFlags.Hidden | SDL.WindowFlags.Vulkan)
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

var objPath = Path.Combine(AppContext.BaseDirectory, "smoke-test-cube.obj");
var modelTask = renderer.LoadModel(objPath);
for (var i = 0; i < 50 && !modelTask.IsCompleted; i++)
{
    world.Update(TimeSpan.FromMilliseconds(16));
    Thread.Sleep(10);
}

if (!modelTask.IsCompletedSuccessfully)
{
    Console.Error.WriteLine($"FAIL: model load did not complete successfully: {modelTask.Exception}");
    return 1;
}

var parts = modelTask.Result;
if (parts.Count != 2)
{
    Console.Error.WriteLine($"FAIL: expected 2 model parts (multi-material cube), got {parts.Count}");
    return 1;
}

var perspectiveCameraEntity = world.Commands.CreateEntity();
world.Commands.AddComponent(perspectiveCameraEntity, new Transform { Position = new Vector3(0, 0, -5), Rotation = Quaternion.Identity, Scale = Vector3.One });
world.Commands.AddComponent(perspectiveCameraEntity, new Camera(Order: 1, ProjectionMode.Perspective, ClearOnBegin: false, MathF.PI / 4f, 0.1f, 100f));
world.ApplyCommands();

world.Commands.CreateEntity(parts.ToEntityTemplate()).AddTransform(Transform.Identity);
world.ApplyCommands();

var framesBeforeFinalDraws = renderer.FrameInFlight.CurrentFrame;
for (var i = 0; i < 5; i++)
    world.Update(TimeSpan.FromMilliseconds(16));

if (renderer.FrameInFlight.CurrentFrame - framesBeforeFinalDraws < 5)
{
    Console.Error.WriteLine($"FAIL: expected 5 more frames submitted after spawning the model, got {renderer.FrameInFlight.CurrentFrame - framesBeforeFinalDraws}");
    return 1;
}

world.RemoveSystem(renderer);
world.RemoveSystem(world.GetSystem<PlatformSystem>());

Console.WriteLine("OK: NativeAOT SDL_GPU sprite + multi-material mesh draw (device/swapchain/shaders/texture/mesh/instance buffers, Assimp) succeeded");
return 0;
