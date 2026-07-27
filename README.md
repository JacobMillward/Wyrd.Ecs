# Wyrd.Ecs

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![.NET 10](https://img.shields.io/badge/.NET-10-blueviolet)

An archetype-based ECS for .NET 10, built around source generation and first-class persistence.

> Pre-release. APIs are still moving and nothing is published to NuGet yet.

- Archetype storage: entities with the same component/tag set live in the same dense arrays
- A fluent, generator-backed query chain of unbounded arity: `world.Query().With<Writes<T>>().With<Reads<U>>().Without<X>().Any<A, B>().ForEach(...)`, no boxing or reflection on the hot path, no arity cap
- `QuerySystem` sugar for the declared-system case: write a `Build` (query shape) and `Execute` (per-entity body) method, the generator fills in dispatch
- A static parallel scheduler: `WorldBuilder.WithSystems(...)` partitions systems into stages with no read/write conflicts, then `ScheduledExecutor` runs each stage inline or on the thread pool depending on world size
- All structural mutation goes through `CommandBuffer`, deferred and applied in one deterministic pass, so it never invalidates a query mid-iteration
- Tick-based change tracking, opt in per component type
- Structural change observers for entity/component add/remove
- AOT-compatible throughout
- [Easy add-in persistence](docs/persistence.md): mark your components, reference a codec package, one method on `WorldBuilder`. Pick binary or JSON, then optionally layer on a continuous WAL for crash-safe incremental saves

## Quick start

```csharp
using Wyrd.Ecs;

public struct Energy : IComponent
{
    public float Current;
    public float DrainPerSecond;
}

// partial: the generator fills in OnUpdate from Build + Execute
public sealed partial class EnergyDrainSystem : QuerySystem
{
    private static Query<(Writes<Energy>, Nil)> Build(World world) => world.Query().With<Writes<Energy>>();

    private partial void Execute(ulong tick, ref Energy energy) => energy.Current -= energy.DrainPerSecond;
}

var world = new World();

var entity = world.Commands.CreateEntity();
world.Commands.AddComponent(entity, new Energy { Current = 100, DrainPerSecond = 1 });
world.ApplyCommands();

new EnergyDrainSystem().RunOnce(world, tick: 1);
```

No system class needed for a one-off query: the same chain works directly against a `World`.

```csharp
world.Query().With<Writes<Energy>>()
    .ForEach(0, (int _, ref Energy energy) => energy.Current -= energy.DrainPerSecond);
```

To run several systems together, register them with `WorldBuilder` and let the scheduler figure out what can run concurrently:

```csharp
var (world, executor) = new WorldBuilder()
    .WithSystems(Wyrd.Ecs.Generated.GeneratedSystemAccess.Entries, new EnergyDrainSystem(), /* ... */)
    .BuildWithExecutor();

executor.RunTick(world, tick: 1);
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
