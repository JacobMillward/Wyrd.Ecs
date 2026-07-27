# Wyrd.Ecs

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![.NET 10](https://img.shields.io/badge/.NET-10-blueviolet)

An archetype-based ECS for .NET 10, built around source generation and first-class persistence.

> Pre-release. APIs are still moving and nothing is published to NuGet yet.

- Archetype storage. Entities with the same components live together in dense arrays.
- A fluent, generator-backed query chain: `world.Query().With<Writes<T>>().With<Reads<U>>().Without<X>().Any<A, B>().ForEach(...)`. Query for as much as you need, no limit.

  > No boxing, no reflection on the hot path.
- `QuerySystem` sugar for the declared-system case. Write a `Build` (query shape) and `Execute` (per-entity body) method, the generator fills in dispatch.
- Systems run in parallel automatically. Register them with `WorldBuilder.WithSystems(...)`, call `World.Tick(...)`, and independent systems spread across your CPU cores with no thread code of your own.

  > The scheduler looks at what each system reads and writes, groups the ones with no conflicts, and runs each group inline or on the thread pool depending on world size.
- All structural mutation goes through `CommandBuffer`, deferred and applied in one deterministic pass. A query never sees a half-finished change mid-iteration.
- Tick-based change tracking, opt in per component type.
- Structural change observers for entity/component add/remove.
- AOT-compatible throughout.
- [Easy add-in persistence](docs/persistence.md). Mark your components, reference a codec package, one method on `WorldBuilder`. Pick binary or JSON, then optionally layer on a continuous WAL for crash-safe incremental saves.

## Quick start

```csharp
using Wyrd.Ecs;

public struct Position : IComponent { public float X; public float Y; }
public struct Velocity : IComponent { public float X; public float Y; }

// partial: the generator fills in OnUpdate from Build + Execute
public sealed partial class MovementSystem : QuerySystem
{
    private static IQueryDefinition Build(World world) => world.Query().With<Writes<Position>>().With<Reads<Velocity>>();

    private partial void Execute(Time time, ref Position position, in Velocity velocity)
    {
        position.X += velocity.X * (float)time.Delta.TotalSeconds;
        position.Y += velocity.Y * (float)time.Delta.TotalSeconds;
    }
}

var world = new World();

var entity = world.Commands.CreateEntity();
world.Commands.AddComponent(entity, new Position());
world.Commands.AddComponent(entity, new Velocity { X = 1, Y = 0 });
world.ApplyCommands();

world.RunOnce(new MovementSystem(), TimeSpan.FromSeconds(1.0 / 60));
```

No system class needed for a one-off query: the same chain works directly against a `World`.

```csharp
world.Query().With<Writes<Position>>().With<Reads<Velocity>>()
    .ForEach(0, (int _, ref Position position, in Velocity velocity) =>
    {
        position.X += velocity.X;
        position.Y += velocity.Y;
    });
```

To run several systems together, register them with `WorldBuilder` and let the scheduler figure out what can run concurrently:

```csharp
var world = new WorldBuilder()
    .WithSystems<MovementSystem>()
    .Build();

world.Tick(TimeSpan.FromSeconds(1.0 / 60));
```

## Project layout

```
src/Core/          World, entities, archetype storage, the query chain, command buffer, the scheduler, and the source generators behind them
src/Persistence/    Snapshot persistence core, and the Binary, Json, and Continuous packages built on it
benchmarks/         BenchmarkDotNet suites, including head-to-head comparisons against Friflo.Engine.ECS and fennecs
```

Each package under `src/` has a matching `.Tests` project alongside it.

## Requirements

.NET 10 SDK. Uses current C# language features throughout, including extension members and `allows ref struct`.

## Known gaps

- No published package. Reference the projects directly until a release goes out.

## License

MIT, see [LICENSE](LICENSE).
