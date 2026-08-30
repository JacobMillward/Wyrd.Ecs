using SDL3;
using Wyrd.Ecs;
using Wyrd.Ecs.Audio;
using Wyrd.Ecs.Debug;
using Wyrd.Ecs.Examples.Asteroids;
using Wyrd.Ecs.Examples.Asteroids.Components;
using Wyrd.Ecs.Examples.Asteroids.Systems;
using Wyrd.Ecs.Examples.Asteroids.Templates;
using Wyrd.Ecs.Input;
using Wyrd.Ecs.Persistence.Json;
using Wyrd.Ecs.Platform;
using Wyrd.Ecs.Renderer;

var world = new WorldBuilder()
    .AddWindow("Asteroids", 960, 720)
    .AddRenderer()
    .AddTransformSystem()
    .AddInput(Bindings.Default())
    .AddAudio()
    .AddJsonPersistence("save.json")
    .AddSystem<ShipControlSystem>()
    .AddSystem<WeaponSystem>()
    .AddSystem<MovementSystem>()
    .AddSystem<WraparoundSystem>()
    .AddSystem<SpinSystem>()
    .AddSystem<CollisionSystem>()
    .AddSystem<SplitSystem>()
    .AddSystem<AudioCueSystem>()
    .AddSystem<ScoreSystem>()
    .AddSystem<GameOverSystem>()
    .AddSystem<PauseSystem>()
    .AddSystem<SaveLoadSystem>()
    .AddSystem<ResetSystem>()
    .AddSystem<LifetimeSystem>()
    .Build();

var renderer = world.GetSystem<RendererSystem>();
var shipTexture = renderer.LoadTexture(Path.Combine(AppContext.BaseDirectory, "Assets", "ship.png"));
var asteroidTexture = renderer.LoadTexture(Path.Combine(AppContext.BaseDirectory, "Assets", "asteroid.png"));
var bulletTexture = renderer.LoadTexture(Path.Combine(AppContext.BaseDirectory, "Assets", "bullet.png"));
var flameTexture = renderer.LoadTexture(Path.Combine(AppContext.BaseDirectory, "Assets", "flame.png"));

var audio = world.GetResource<AudioPlayer>();
var laserSound = audio.LoadSound(Path.Combine(AppContext.BaseDirectory, "Assets", "laser.wav"));
var explosionSound = audio.LoadSound(Path.Combine(AppContext.BaseDirectory, "Assets", "explosion.wav"));
var engineSound = audio.LoadSound(Path.Combine(AppContext.BaseDirectory, "Assets", "engine.wav"));

var shipTemplate = new ShipTemplate(shipTexture, flameTexture);
var bulletTemplate = new BulletTemplate(bulletTexture);
var asteroidTemplate = new AsteroidTemplate(asteroidTexture);

// Handles above are usable immediately (LoadTexture/LoadSound return them synchronously).
// Registering GameAssets before WaitForLoads lets WeaponSystem (Variable cadence, so it runs
// during WaitForLoads' own world.Update calls too) find the resource on its very first tick,
// before the ship entity exists to make its query match anything.
world.AddResource(new GameAssets(shipTemplate, bulletTemplate, asteroidTemplate, laserSound, explosionSound, engineSound));

WaitForLoads(
    world,
    renderer.WaitForLoadAsync(shipTexture),
    renderer.WaitForLoadAsync(asteroidTexture),
    renderer.WaitForLoadAsync(bulletTexture),
    renderer.WaitForLoadAsync(flameTexture),
    audio.WaitForLoadAsync(laserSound),
    audio.WaitForLoadAsync(explosionSound),
    audio.WaitForLoadAsync(engineSound));

var camera = world.Commands.CreateEntity();
world.Commands.AddComponent(camera, Transform.Identity);
world.Commands.AddComponent(camera, new OrthographicCamera(Order: 0, ClearOnBegin: true, Size: Playfield.HalfHeight, Near: 0.1f, Far: 100f));

world.Commands.CreateEntity(shipTemplate);

var gameState = world.Commands.CreateEntity();
world.Commands.AddTag<Game>(gameState);
world.Commands.AddComponent(gameState, new Score());

AsteroidSpawner.SpawnInitialWave(world.Commands, asteroidTemplate, new Random());

world.ApplyCommands();

var debugServer = args.Contains("--debug") ? world.WithDebugServer() : null;
try
{
    var platform = world.GetSystem<PlatformSystem>();
    var clock = System.Diagnostics.Stopwatch.StartNew();
    var lastElapsed = clock.Elapsed;
    while (!platform.Events.Any(e => e.Type == (uint)SDL.EventType.Quit))
    {
        var elapsed = clock.Elapsed;
        world.Update(elapsed - lastElapsed);
        lastElapsed = elapsed;
    }
}
finally
{
    debugServer?.Dispose();
}

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
