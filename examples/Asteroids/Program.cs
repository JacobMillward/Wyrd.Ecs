using SDL3;
using Wyrd.Ecs;
using Wyrd.Ecs.Examples.Asteroids;
using Wyrd.Ecs.Platform;
using Wyrd.Ecs.Renderer;

var world = new WorldBuilder()
    .AddWindow("Asteroids", 960, 720)
    .AddRenderer()
    .Build();

var renderer = world.GetSystem<RendererSystem>();
var shipTexture = renderer.LoadTexture(Path.Combine(AppContext.BaseDirectory, "Assets", "ship.png"));
var asteroidTexture = renderer.LoadTexture(Path.Combine(AppContext.BaseDirectory, "Assets", "asteroid.png"));
var bulletTexture = renderer.LoadTexture(Path.Combine(AppContext.BaseDirectory, "Assets", "bullet.png"));
var flameTexture = renderer.LoadTexture(Path.Combine(AppContext.BaseDirectory, "Assets", "flame.png"));

WaitForLoads(
    world,
    renderer.WaitForLoadAsync(shipTexture),
    renderer.WaitForLoadAsync(asteroidTexture),
    renderer.WaitForLoadAsync(bulletTexture),
    renderer.WaitForLoadAsync(flameTexture));

var camera = world.Commands.CreateEntity();
world.Commands.AddComponent(camera, Transform.Identity);
world.Commands.AddComponent(camera, new OrthographicCamera(Order: 0, ClearOnBegin: true, Size: Playfield.HalfHeight, Near: 0.1f, Far: 100f));

var ship = world.Commands.CreateEntity();
world.Commands.AddComponent(ship, Transform.Identity);
world.Commands.AddComponent(ship, new Sprite(SourceRect: null, Tint: Color.White));
world.Commands.AddComponent(ship, new Material(ShaderKind.UnlitSprite, shipTexture));
world.ApplyCommands();

var platform = world.GetSystem<PlatformSystem>();
while (!platform.Events.Any(e => e.Type == (uint)SDL.EventType.Quit))
    world.Update(TimeSpan.FromMilliseconds(16));

static void WaitForLoads(World world, params Task[] tasks)
{
    var all = Task.WhenAll(tasks);
    while (!all.IsCompleted)
    {
        world.Update(TimeSpan.FromMilliseconds(16));
        Thread.Sleep(1);
    }
    all.GetAwaiter().GetResult();
}
