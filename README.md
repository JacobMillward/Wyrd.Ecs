# Wyrd.Ecs

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![.NET 10](https://img.shields.io/badge/.NET-10-blueviolet)
[![Docs](https://img.shields.io/badge/docs-wyrd.millward.dev-e3a35d)](https://wyrd.millward.dev)

Wyrd is a game engine for .NET 10. This repo is its ECS core: source-generated queries, parallel systems by default, and persistence you can turn on with one line.

> Pre-release. APIs are still moving. Packages build locally with `dotnet pack`, but nothing is published to NuGet yet.

Full guides and reference live at **[wyrd.millward.dev](https://wyrd.millward.dev)**; this README is the short version.

## Features

- Archetype storage. Entities with the same components live together in dense arrays, no boxing, no reflection on the hot path.
- [Source-generated queries](#queries), a fluent chain that reads and writes components straight off the callback's `ref`/`in` parameters.
- [`QuerySystem`](#quick-start) declares a query and a per-entity body; the generator fills in dispatch.
- [Systems run in parallel automatically](#systems), and can be reordered, enabled, or swapped at runtime.
- [Fixed timestep, pause, and timescale](#timestep-pause-and-timescale) share one clock.
- [Relations and hierarchy](#relations-and-hierarchy): structural edges between entities, parent/child ships built in.
- [Reusable prefabs](#quick-start) via `EntityTemplate`.
- [Events](#events) for one-off signals between systems that don't know about each other.
- Tick-based change tracking and structural change observers, opt in per component type.
- [A live debug UI](#debug-ui) for browsing world state as your game runs.
- AOT-compatible throughout, fully source-generated, no reflection.
- [Add-in persistence](https://wyrd.millward.dev/guides/persistence/): binary or JSON, one method on `WorldBuilder`, optional continuous WAL.

## Queries

```csharp
world.Query().With<Position, Velocity>().Without<Frozen>()
    .ForEach((ref position, in velocity) => { /* ... */ });
```

`.With<A, B, C>()` collapses up to eight types into one call, and the same collapse works for `.Without`/`.Has`/`.Any`. Filters apply conditionally without changing the query's shape:

```csharp
if (hardcore) query = query.Has<HardcoreOnly>();
```

Full chain syntax in the [Queries guide](https://wyrd.millward.dev/guides/queries/).

## Quick start

```csharp
using Wyrd.Ecs;

public struct Position : IComponent { public float X; public float Y; }
public struct Velocity : IComponent { public float X; public float Y; }

public sealed partial class MovementSystem : QuerySystem
{
    protected override IQuery DefineQuery(Query query) => query.With<Position>().With<Velocity>();

    public void Update(Time time, ref Position position, in Velocity velocity)
    {
        position.X += velocity.X * (float)time.Delta.TotalSeconds;
        position.Y += velocity.Y * (float)time.Delta.TotalSeconds;
    }
}

var world = new World();

world.Commands.CreateEntity()
    .AddComponent(new Position())
    .AddComponent(new Velocity { X = 1, Y = 0 });
world.ApplyCommands();

world.RunOnce(new MovementSystem(), TimeSpan.FromSeconds(1.0 / 60));
```

To run several systems together, register them with `WorldBuilder` and let the scheduler figure out what can run concurrently:

```csharp
var world = new WorldBuilder()
    .AddSystem<MovementSystem>()
    .Build();

world.Update(TimeSpan.FromSeconds(1.0 / 60));
```

Prefabs are `EntityTemplate`s: declare components, tags, and child subtrees once, then instantiate as many times as needed.

```csharp
var thrusterTemplate = new EntityTemplate().AddComponent(new Position());

var shipTemplate = new EntityTemplate()
    .AddComponent(new Position())
    .AddChild(thrusterTemplate)
    .AddChild(thrusterTemplate);

var ship = world.Commands.CreateEntity(shipTemplate);
world.ApplyCommands();

foreach (var thruster in world.Children(ship))
{
    // ...
}
```

`CreateEntity(template, count)` instantiates many childless entities from one template in a single batch.

## Systems

The scheduler looks at what each system reads and writes, groups the ones with no conflicts, and runs each group inline or on the thread pool depending on world size, no thread code of your own.

Systems can be enabled/disabled (`system.Enabled = false`), added, or removed at runtime, `world.AddSystem<T>()`/`world.RemoveSystem<T>()` work the same as at `Build()` time, and ordering edges resolve correctly regardless of registration order. `[RunBefore]`/`[RunAfter]` force an explicit order between two systems when the scheduler wouldn't otherwise separate them. The scheduler itself is swappable via `WorldBuilder.WithScheduler(...)` for a custom (e.g. deterministic) implementation.

## Timestep, pause, and timescale

```csharp
[FixedTimestep]
public sealed partial class PhysicsSystem : QuerySystem { /* ... */ }

world.Pause();  // every system's Time.Delta becomes zero
world.TimeScale = 0.25; // slow motion, applies underneath a pause too
```

Details in the [Timestep, Pause & Timescale guide](https://wyrd.millward.dev/guides/timestep-pause-timescale/).

## Relations and hierarchy

Relations are a third category alongside components and tags, structural edges between two entities:

```csharp
goblin.AddRelation<Targeting>(player, new Targeting { ThreatLevel = 0.8f });
```

Parent/child hierarchy ships as one built-in relation: `entity.SetParent(parent)`/`AddChild(child)`, `world.Children(e)`/`Ancestors(e)`/`Descendants(e)` to walk it. Destroying a parent recursively destroys its children. More in the [Relations guide](https://wyrd.millward.dev/guides/relations/).

## Events

```csharp
public struct DamageDealt : IEvent { public Entity Target; public int Amount; }

world.Emit(new DamageDealt { Target = goblin, Amount = 5 });
```

`Emit` is immediate, no `CommandBuffer` involved, and safe to call concurrently from several systems in the same parallel stage. Details in the [Events guide](https://wyrd.millward.dev/guides/events/).

## Debug UI

```csharp
using var server = world.WithDebugServer();
```

Serves a browser panel at `http://127.0.0.1:5299` showing every archetype, every live entity, a selected entity's components with editable fields, and a running log of structural changes, driven off the same `World` your game is running. Details in the [Debugging guide](https://wyrd.millward.dev/guides/debugging/).

## Project layout

```
src/Core/         World, entities, archetype storage, the query chain, command buffer, the scheduler, and the source generators behind them
src/Debug/        Wyrd.Ecs.Debug, the live browser debug UI and the programmatic inspection API it's built on
src/Persistence/  Snapshot persistence core, and the Binary, Json, and Continuous packages built on it
docs/             The Starlight docs site published at wyrd.millward.dev
benchmarks/       BenchmarkDotNet suites, including head-to-head comparisons against Friflo.Engine.ECS and fennecs
```

Each package under `src/` has a matching `.Tests` project alongside it.

## Benchmarks

```
dotnet run -c Release --project benchmarks/Wyrd.Ecs.Benchmarks
```

Runs BenchmarkDotNet's interactive picker over every benchmark in the project. Pass `--filter` to run a subset without the picker, e.g. `--filter *TrackedQueryIteration*` or `--filter Comparison.*` for the head-to-head suites against Friflo.Engine.ECS and fennecs. BenchmarkDotNet refuses to run outside a `Release` build, so `-c Release` is required.

## Requirements

.NET 10 SDK. Uses current C# language features throughout, including extension members and `allows ref struct`.

## Known gaps

- No published package yet. `dotnet pack` produces installable packages for `Wyrd.Ecs`, `Wyrd.Ecs.Debug`, `Wyrd.Ecs.Debug.Abstractions`, `Wyrd.Ecs.Persistence`, `Wyrd.Ecs.Persistence.Binary`, `Wyrd.Ecs.Persistence.Json`, and `Wyrd.Ecs.Persistence.Continuous`, but none are published to nuget.org yet. Reference the projects directly until a release goes out.
- No renderer, audio, or asset pipeline yet.

## License

MIT, see [LICENSE](LICENSE).
