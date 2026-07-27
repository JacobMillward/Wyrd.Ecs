using BenchmarkDotNet.Attributes;
using fennecs;
using Comparison.Fennecs;

namespace Comparison.EntityLifecycle;

public partial class EntityLifecycleBenchmarks
{
    private sealed class FennecsContext
    {
        public readonly World World = new();

        /// <summary>Reused scratch space for <see cref="Fennecs_DisposeEntity"/> — sized once, never reallocated, so it doesn't contaminate that method's own allocation measurement.</summary>
        public readonly Entity[] DisposeScratch = new Entity[EntityCount];
    }

    [Context] private FennecsContext _fennecs = null!;

    /// <summary>Resets <see cref="_fennecs"/> every iteration for the growing <c>Create*</c> methods — see <see cref="Wyrd_ResetContext"/>'s docs for why.</summary>
    [IterationSetup(Targets = [
        nameof(Fennecs_CreateBareEntity), nameof(Fennecs_CreateOneComponentEntity),
        nameof(Fennecs_CreateFourComponentEntity), nameof(Fennecs_CreateEightComponentEntity)])]
    public void Fennecs_ResetContext() => _fennecs = new FennecsContext();

    [Benchmark(OperationsPerInvoke = EntityCount)]
    public void Fennecs_CreateBareEntity()
    {
        for (var i = 0; i < EntityCount; i++)
            _fennecs.World.Spawn();
    }

    [Benchmark(OperationsPerInvoke = EntityCount)]
    public void Fennecs_CreateOneComponentEntity()
    {
        for (var i = 0; i < EntityCount; i++)
            _fennecs.World.Spawn().Add(new Position());
    }

    [Benchmark(OperationsPerInvoke = EntityCount)]
    public void Fennecs_CreateFourComponentEntity()
    {
        for (var i = 0; i < EntityCount; i++)
            _fennecs.World.Spawn().Add(new Position()).Add(new Velocity()).Add(new Health()).Add(new BulkPayload());
    }

    [Benchmark(OperationsPerInvoke = EntityCount)]
    public void Fennecs_CreateEightComponentEntity()
    {
        for (var i = 0; i < EntityCount; i++)
            _fennecs.World.Spawn()
                .Add(new Position()).Add(new Velocity()).Add(new Health()).Add(new BulkPayload())
                .Add(new Padding1()).Add(new Padding2()).Add(new Padding3()).Add(new Padding4());
    }

    /// <summary>Create-then-destroy pairs, self-resetting — see <see cref="EntityLifecycleBenchmarks.Wyrd_DisposeEntity"/>'s docs for why.</summary>
    [Benchmark(OperationsPerInvoke = EntityCount)]
    public void Fennecs_DisposeEntity()
    {
        for (var i = 0; i < EntityCount; i++)
            _fennecs.DisposeScratch[i] = _fennecs.World.Spawn();

        for (var i = 0; i < EntityCount; i++)
            _fennecs.DisposeScratch[i].Despawn();
    }
}
