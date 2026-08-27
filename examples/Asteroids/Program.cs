using System.Numerics;
using SDL3;
using Wyrd.Ecs;
using Wyrd.Ecs.Examples.Asteroids;
using Wyrd.Ecs.Examples.Asteroids.Components;
using Wyrd.Ecs.Examples.Asteroids.Systems;
using Wyrd.Ecs.Input;
using Wyrd.Ecs.Platform;
using Wyrd.Ecs.Renderer;

var world = new WorldBuilder()
    .AddWindow("Asteroids", 960, 720)
    .AddRenderer()
    .AddTransformSystem()
    .AddInput(Bindings.Default())
    .AddSystem<ShipControlSystem>()
    .AddSystem<MovementSystem>()
    .AddSystem<WraparoundSystem>()
    .AddSystem<SpinSystem>()
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

var shipTemplate = new EntityTemplate()
    .AddTransform(Vector3.Zero)
    .AddComponent(new Velocity())
    .AddComponent(new Ship())
    .AddComponent(new Sprite(SourceRect: null, Tint: Color.White))
    .AddComponent(new Material(ShaderKind.UnlitSprite, shipTexture))
    .AddChild(new EntityTemplate()
        .AddTransform(new Transform { Position = new Vector3(-28f, 0f, 0f), Rotation = Quaternion.Identity, Scale = Vector3.Zero })
        .AddComponent(new Sprite(SourceRect: null, Tint: Color.White))
        .AddComponent(new Material(ShaderKind.UnlitSprite, flameTexture))
        .AddTag<EngineFlame>());

world.Commands.CreateEntity(shipTemplate);

var testAsteroid1 = world.Commands.CreateEntity();
world.Commands.AddComponent(testAsteroid1, new Transform { Position = new Vector3(-240f, 90f, 0f), Rotation = Quaternion.Identity, Scale = Vector3.One * AsteroidSize.Large.Scale() });
world.Commands.AddComponent(testAsteroid1, new Velocity { Value = new Vector3(90f, -30f, 0f) });
world.Commands.AddComponent(testAsteroid1, new Spin { RadiansPerSecond = 0.6f });
world.Commands.AddComponent(testAsteroid1, new Asteroid { Size = AsteroidSize.Large });
world.Commands.AddComponent(testAsteroid1, new Sprite(SourceRect: null, Tint: Color.White));
world.Commands.AddComponent(testAsteroid1, new Material(ShaderKind.UnlitSprite, asteroidTexture));

var testAsteroid2 = world.Commands.CreateEntity();
world.Commands.AddComponent(testAsteroid2, new Transform { Position = new Vector3(180f, -150f, 0f), Rotation = Quaternion.Identity, Scale = Vector3.One * AsteroidSize.Small.Scale() });
world.Commands.AddComponent(testAsteroid2, new Velocity { Value = new Vector3(-60f, 75f, 0f) });
world.Commands.AddComponent(testAsteroid2, new Spin { RadiansPerSecond = -1.4f });
world.Commands.AddComponent(testAsteroid2, new Asteroid { Size = AsteroidSize.Small });
world.Commands.AddComponent(testAsteroid2, new Sprite(SourceRect: null, Tint: Color.White));
world.Commands.AddComponent(testAsteroid2, new Material(ShaderKind.UnlitSprite, asteroidTexture));

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
