# Wyrd.Ecs

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![.NET 10](https://img.shields.io/badge/.NET-10-blueviolet)

An archetype-based ECS for .NET 10, built around source generation and first-class persistence.

> Pre-release. APIs are still moving. Packages build locally with `dotnet pack`, but nothing is published to NuGet yet.

- Archetype storage. Entities with the same components live together in dense arrays.
- A fluent, generator-backed query chain: `world.Query().With<T>().With<U>().Without<X>().Any<A, B>().ForEach(...)`. Query for as much as you need, no limit. Each component's read/write access comes from the `ref`/`in` on the callback itself — nothing to declare twice. `.With<A, B, C>()` collapses what would otherwise be three chained calls into one (also works for `.Without`/`.Has`/`.Any`, up to 8 at a time).

  > No boxing, no reflection on the hot path.
- `.Without`/`.Has`/`.Any` can be applied conditionally — they never change the query's shape, only its runtime filter, so `if (hardcore) query = query.Has<HardcoreOnly>();` compiles and works exactly as you'd expect.
- `QuerySystem` sugar for the declared-system case. Override `DefineQuery` (query shape) and declare an `Update` (per-entity body) method, the generator fills in dispatch.
- Systems run in parallel automatically. Register them with `WorldBuilder.AddSystem<T>()`, call `World.Update(...)`, and independent systems spread across your CPU cores with no thread code of your own.

  > The scheduler looks at what each system reads and writes, groups the ones with no conflicts, and runs each group inline or on the thread pool depending on world size.
- Systems can be enabled/disabled (`system.Enabled = false`), added, or removed at runtime — `world.AddSystem<T>()`/`world.RemoveSystem<T>()` work the same as at `Build()` time, ordering edges resolve correctly regardless of registration order, and the scheduler itself is swappable via `WorldBuilder.WithScheduler(...)` for a custom (e.g. deterministic) implementation.
- All structural mutation goes through `CommandBuffer`, deferred and applied in one deterministic pass. A query never sees a half-finished change mid-iteration.
- `CreateEntity()` hands back an `EntityView`, a chainable handle for the entity you just made: `commands.CreateEntity().AddComponent(pos).AddTag<Enemy>()`. It converts implicitly to `Entity` when that's all you need.
- Parent/child hierarchy built in. `entity.SetParent(parent)` / `AddChild(child)`, `world.Children(e)` / `Ancestors(e)` / `Descendants(e)` to walk it. Destroying a parent recursively destroys its children.
- `EntityTemplate` for reusable prefab definitions, components/tags/child subtrees declared once. Instantiate a whole tree in one call with `commands.CreateEntity(template)`, or many at once with `commands.CreateEntity(template, count)`.
- Tick-based change tracking, opt in per component type.
- Structural change observers for entity/component add/remove.
- AOT-compatible throughout.
- [Easy add-in persistence](https://wyrd.millward.dev/guides/persistence/). Mark your components, reference a codec package, one method on `WorldBuilder`. Pick binary or JSON, then optionally layer on a continuous WAL for crash-safe incremental saves.

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

No system class needed for a one-off query: the same chain works directly against a `World`.

```csharp
world.Query().With<Position>().With<Velocity>()
    .ForEach((ref position, in velocity) =>
    {
        position.X += velocity.X;
        position.Y += velocity.Y;
    });
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

- No published package yet. `dotnet pack` produces installable packages for `Wyrd.Ecs`, `Wyrd.Ecs.Persistence`, `Wyrd.Ecs.Persistence.Binary`, `Wyrd.Ecs.Persistence.Json`, and `Wyrd.Ecs.Persistence.Continuous`, but none are published to nuget.org yet. Reference the projects directly until a release goes out.

## License

MIT, see [LICENSE](LICENSE).
