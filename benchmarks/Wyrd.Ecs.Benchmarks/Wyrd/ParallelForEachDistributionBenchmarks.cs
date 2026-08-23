using BenchmarkDotNet.Attributes;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

public struct PfdPosition : IComponent { public float X; }

// One distinct tag per archetype: archetype identity is the component-type set, so these
// split the many-small-archetypes layout into SmallArchetypes archetypes holding only
// PfdPosition data.
public struct PfdTag00 : ITag { }
public struct PfdTag01 : ITag { }
public struct PfdTag02 : ITag { }
public struct PfdTag03 : ITag { }
public struct PfdTag04 : ITag { }
public struct PfdTag05 : ITag { }
public struct PfdTag06 : ITag { }
public struct PfdTag07 : ITag { }
public struct PfdTag08 : ITag { }
public struct PfdTag09 : ITag { }
public struct PfdTag10 : ITag { }
public struct PfdTag11 : ITag { }
public struct PfdTag12 : ITag { }
public struct PfdTag13 : ITag { }
public struct PfdTag14 : ITag { }
public struct PfdTag15 : ITag { }
public struct PfdTag16 : ITag { }
public struct PfdTag17 : ITag { }
public struct PfdTag18 : ITag { }
public struct PfdTag19 : ITag { }
public struct PfdTag20 : ITag { }
public struct PfdTag21 : ITag { }
public struct PfdTag22 : ITag { }
public struct PfdTag23 : ITag { }
public struct PfdTag24 : ITag { }
public struct PfdTag25 : ITag { }
public struct PfdTag26 : ITag { }
public struct PfdTag27 : ITag { }
public struct PfdTag28 : ITag { }
public struct PfdTag29 : ITag { }
public struct PfdTag30 : ITag { }
public struct PfdTag31 : ITag { }

/// <summary>
/// Compares generated <c>.ParallelForEach</c> against sequential <c>.ForEach</c> doing
/// identical per-row mutation work across three world layouts. The large single archetype
/// is the under-parallelized case: <c>Parallel.ForEach</c> receives one chunk per archetype,
/// so 100k rows in one archetype dispatch as a single task. Many small archetypes already
/// parallelize per archetype. The small world guards dispatch overhead where parallelism
/// cannot pay for itself.
/// </summary>
[MemoryDiagnoser]
public class ParallelForEachDistributionBenchmarks
{
    public const int LargeArchetypeEntities = 100_000;
    public const int SmallArchetypes = 32;
    public const int EntitiesPerSmallArchetype = 3_000;
    public const int SmallWorldEntities = 1_000;

    private World _largeSingleArchetypeWorld = null!;
    private World _manySmallArchetypesWorld = null!;
    private World _smallWorld = null!;

    [GlobalSetup]
    public void Setup()
    {
        _largeSingleArchetypeWorld = new World();
        SeedPlain(_largeSingleArchetypeWorld, LargeArchetypeEntities);

        _manySmallArchetypesWorld = new World();
        SeedTag<PfdTag00>(_manySmallArchetypesWorld);
        SeedTag<PfdTag01>(_manySmallArchetypesWorld);
        SeedTag<PfdTag02>(_manySmallArchetypesWorld);
        SeedTag<PfdTag03>(_manySmallArchetypesWorld);
        SeedTag<PfdTag04>(_manySmallArchetypesWorld);
        SeedTag<PfdTag05>(_manySmallArchetypesWorld);
        SeedTag<PfdTag06>(_manySmallArchetypesWorld);
        SeedTag<PfdTag07>(_manySmallArchetypesWorld);
        SeedTag<PfdTag08>(_manySmallArchetypesWorld);
        SeedTag<PfdTag09>(_manySmallArchetypesWorld);
        SeedTag<PfdTag10>(_manySmallArchetypesWorld);
        SeedTag<PfdTag11>(_manySmallArchetypesWorld);
        SeedTag<PfdTag12>(_manySmallArchetypesWorld);
        SeedTag<PfdTag13>(_manySmallArchetypesWorld);
        SeedTag<PfdTag14>(_manySmallArchetypesWorld);
        SeedTag<PfdTag15>(_manySmallArchetypesWorld);
        SeedTag<PfdTag16>(_manySmallArchetypesWorld);
        SeedTag<PfdTag17>(_manySmallArchetypesWorld);
        SeedTag<PfdTag18>(_manySmallArchetypesWorld);
        SeedTag<PfdTag19>(_manySmallArchetypesWorld);
        SeedTag<PfdTag20>(_manySmallArchetypesWorld);
        SeedTag<PfdTag21>(_manySmallArchetypesWorld);
        SeedTag<PfdTag22>(_manySmallArchetypesWorld);
        SeedTag<PfdTag23>(_manySmallArchetypesWorld);
        SeedTag<PfdTag24>(_manySmallArchetypesWorld);
        SeedTag<PfdTag25>(_manySmallArchetypesWorld);
        SeedTag<PfdTag26>(_manySmallArchetypesWorld);
        SeedTag<PfdTag27>(_manySmallArchetypesWorld);
        SeedTag<PfdTag28>(_manySmallArchetypesWorld);
        SeedTag<PfdTag29>(_manySmallArchetypesWorld);
        SeedTag<PfdTag30>(_manySmallArchetypesWorld);
        SeedTag<PfdTag31>(_manySmallArchetypesWorld);

        _smallWorld = new World();
        SeedPlain(_smallWorld, SmallWorldEntities);

        _largeSingleArchetypeWorld.ApplyCommands();
        _manySmallArchetypesWorld.ApplyCommands();
        _smallWorld.ApplyCommands();
    }

    private void SeedPlain(World world, int count)
    {
        for (var i = 0; i < count; i++)
            world.Commands.CreateEntity(new PfdPosition { X = i });
    }

    private void SeedTag<TTag>(World world) where TTag : struct, ITag
    {
        for (var i = 0; i < EntitiesPerSmallArchetype; i++)
        {
            var e = world.Commands.CreateEntity(new PfdPosition { X = i });
            world.Commands.AddTag<TTag>(e);
        }
    }

    [Benchmark(Baseline = true)]
    public void ForEach_LargeSingleArchetype() =>
        _largeSingleArchetypeWorld.Query().With<PfdPosition>().ForEach(0, (in int _, ref PfdPosition p) => p.X += 1f);

    [Benchmark]
    public void ParallelForEach_LargeSingleArchetype() =>
        _largeSingleArchetypeWorld.Query().With<PfdPosition>().ParallelForEach(0, (in int _, ref PfdPosition p) => p.X += 1f);

    [Benchmark]
    public void ForEach_ManySmallArchetypes() =>
        _manySmallArchetypesWorld.Query().With<PfdPosition>().ForEach(0, (in int _, ref PfdPosition p) => p.X += 1f);

    [Benchmark]
    public void ParallelForEach_ManySmallArchetypes() =>
        _manySmallArchetypesWorld.Query().With<PfdPosition>().ParallelForEach(0, (in int _, ref PfdPosition p) => p.X += 1f);

    [Benchmark]
    public void ForEach_SmallWorld() =>
        _smallWorld.Query().With<PfdPosition>().ForEach(0, (in int _, ref PfdPosition p) => p.X += 1f);

    [Benchmark]
    public void ParallelForEach_SmallWorld() =>
        _smallWorld.Query().With<PfdPosition>().ParallelForEach(0, (in int _, ref PfdPosition p) => p.X += 1f);
}
