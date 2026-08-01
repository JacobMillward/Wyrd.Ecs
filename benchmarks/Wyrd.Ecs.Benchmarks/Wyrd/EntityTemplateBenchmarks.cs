using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;
using Comparison.Wyrd;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

/// <summary>
/// Feeds the deferred decision in <c>docs/superpowers/specs/2026-07-31-entity-template-design.md</c>
/// section G: whether a reused <see cref="EntityTemplate"/> costs meaningfully more per
/// instantiate than the equivalent generated <c>CreateEntity&lt;T0..Tn&gt;</c> call, same
/// shape (four components, matching <see cref="TrackedEntityLifecycleBenchmarks.CreateFourComponentEntity"/>).
/// Same <c>[SimpleJob(invocationCount: 1)]</c>/per-iteration world reset reasoning as that
/// class — see its own docs for why.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(invocationCount: 1)]
public class EntityTemplateBenchmarks
{
    private const int EntityCount = Comparison.EntityLifecycle.EntityLifecycleBenchmarks.EntityCount;

    private World _world = null!;
    private EntityTemplate _fourComponentTemplate = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _fourComponentTemplate = new EntityTemplate()
            .AddComponent(new Position())
            .AddComponent(new Velocity())
            .AddComponent(new Health())
            .AddComponent(new BulkPayload());
    }

    [IterationSetup(Targets = [nameof(CreateFourComponentEntity_Arity), nameof(CreateFourComponentEntity_Template), nameof(CreateFourComponentEntities_Batch_Arity), nameof(CreateFourComponentEntities_Batch_Template)])]
    public void ResetWorld() => _world = new World();

    [Benchmark(Baseline = true, OperationsPerInvoke = EntityCount)]
    public void CreateFourComponentEntity_Arity()
    {
        for (var i = 0; i < EntityCount; i++)
            _world.Commands.CreateEntity(new Position(), new Velocity(), new Health(), new BulkPayload());
        _world.ApplyCommands();
    }

    [Benchmark(OperationsPerInvoke = EntityCount)]
    public void CreateFourComponentEntity_Template()
    {
        for (var i = 0; i < EntityCount; i++)
            _world.Commands.CreateEntity(_fourComponentTemplate);
        _world.ApplyCommands();
    }

    [Benchmark(OperationsPerInvoke = EntityCount)]
    public void CreateFourComponentEntities_Batch_Arity()
    {
        _world.Commands.CreateEntity(EntityCount, new Position(), new Velocity(), new Health(), new BulkPayload());
        _world.ApplyCommands();
    }

    [Benchmark(OperationsPerInvoke = EntityCount)]
    public void CreateFourComponentEntities_Batch_Template()
    {
        _world.Commands.CreateEntity(_fourComponentTemplate, EntityCount);
        _world.ApplyCommands();
    }
}
