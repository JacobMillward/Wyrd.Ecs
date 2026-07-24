# Wyrd.Ecs

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![.NET 10](https://img.shields.io/badge/.NET-10-blueviolet)

An archetype-based ECS for .NET 10, built around source generation and first-class persistence.

> Pre-release. APIs are still moving and nothing is published to NuGet yet.

- Archetype storage: entities with the same component/tag set live in the same dense arrays
- Typed `Query<T0..T7>` and `QuerySystem<T0..T7>` generated for arity 1 through 8, no boxing or reflection on the hot path
- All structural mutation goes through `CommandBuffer`, deferred and applied in one deterministic pass, so it never invalidates a query mid-iteration
- Tick-based change tracking, opt in per component type
- Structural change observers for entity/component add/remove
- A `GetComponent` call-site interceptor, plus a Roslyn analyzer that catches a forgotten `ref` on `GetComponent`
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

// partial: the generator fills in OnUpdate from Execute
public sealed partial class EnergyDrainSystem : QuerySystem<Energy>
{
    protected override void Execute(World world, ulong tick, ref Energy energy)
    {
        energy.Current -= energy.DrainPerSecond;
    }
}

var world = new World();

var entity = world.Commands.CreateEntity();
world.Commands.AddComponent(entity, new Energy { Current = 100, DrainPerSecond = 1 });
world.ApplyCommands();

new EnergyDrainSystem().RunOnce(world, tick: 1);
```

## Project layout

```
src/Core/          World, entities, queries, command buffer, source generators, interceptors, analyzers
src/Persistence/    Snapshot persistence, and the Binary, Json, and Continuous packages built on it
benchmarks/         BenchmarkDotNet suites
```

Each package under `src/` has a matching `.Tests` project alongside it.

## Requirements

.NET 10 SDK. Uses current C# language features throughout, including extension members and `allows ref struct`.

## Known gaps

- No scheduler. `RunOnce` invokes one system directly; ordering and running many systems is on the caller for now.
- No published package. Reference the projects directly until a release goes out.

## License

MIT, see [LICENSE](LICENSE).
