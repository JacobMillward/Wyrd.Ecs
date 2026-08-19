using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;
using Comparison.Wyrd;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

/// <summary>
/// Whether a reused <see cref="EntityTemplate"/> costs meaningfully more per instantiate
/// than the equivalent generated <c>CreateEntity&lt;T0..Tn&gt;</c> call, same four-component
/// shape as <see cref="TrackedEntityLifecycleBenchmarks.CreateFourComponentEntity"/>.
/// <c>warmupCount: 50</c> is deliberate: BenchmarkDotNet's adaptive warmup can end before
/// the JIT finishes tiering up, so a fixed, generous warmup measures steady-state
/// performance instead.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(invocationCount: 1, warmupCount: 50)]
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
