using BenchmarkDotNet.Attributes;
using Friflo.Engine.ECS;
using Comparison.Friflo;
// Friflo.Engine.ECS ships its own built-in Position type, colliding with our vocabulary's —
// disambiguate in favor of ours everywhere in this file.
using Position = Comparison.Friflo.Position;

namespace Comparison.EntityLifecycle;

public partial class EntityLifecycleBenchmarks
{
    private sealed class FrifloContext
    {
        public readonly EntityStore Store = new();

        /// <summary>Reused scratch space for <see cref="Friflo_DisposeEntity"/> — sized once, never reallocated, so it doesn't contaminate that method's own allocation measurement.</summary>
        public readonly Entity[] DisposeScratch = new Entity[EntityCount];
    }

    [Context] private FrifloContext _friflo = null!;

    /// <summary>Resets <see cref="_friflo"/> every iteration for the growing <c>Create*</c> methods — see <see cref="Wyrd_ResetContext"/>'s docs for why.</summary>
    [IterationSetup(Targets = [
        nameof(Friflo_CreateBareEntity), nameof(Friflo_CreateOneComponentEntity),
        nameof(Friflo_CreateFourComponentEntity), nameof(Friflo_CreateEightComponentEntity)])]
    public void Friflo_ResetContext() => _friflo = new FrifloContext();

    [Benchmark(OperationsPerInvoke = EntityCount)]
    public void Friflo_CreateBareEntity()
    {
        for (var i = 0; i < EntityCount; i++)
            _friflo.Store.CreateEntity();
    }

    [Benchmark(OperationsPerInvoke = EntityCount)]
    public void Friflo_CreateOneComponentEntity()
    {
        for (var i = 0; i < EntityCount; i++)
            _friflo.Store.CreateEntity(new Position());
    }

    [Benchmark(OperationsPerInvoke = EntityCount)]
    public void Friflo_CreateFourComponentEntity()
    {
        for (var i = 0; i < EntityCount; i++)
            _friflo.Store.CreateEntity(new Position(), new Velocity(), new Health(), new BulkPayload());
    }

    [Benchmark(OperationsPerInvoke = EntityCount)]
    public void Friflo_CreateEightComponentEntity()
    {
        for (var i = 0; i < EntityCount; i++)
            _friflo.Store.CreateEntity(
                new Position(), new Velocity(), new Health(), new BulkPayload(),
                new Padding1(), new Padding2(), new Padding3(), new Padding4());
    }

    /// <summary>Create-then-destroy pairs, self-resetting — see <see cref="EntityLifecycleBenchmarks.Wyrd_DisposeEntity"/>'s docs for why.</summary>
    [Benchmark(OperationsPerInvoke = EntityCount)]
    public void Friflo_DisposeEntity()
    {
        for (var i = 0; i < EntityCount; i++)
            _friflo.DisposeScratch[i] = _friflo.Store.CreateEntity();

        for (var i = 0; i < EntityCount; i++)
            _friflo.DisposeScratch[i].DeleteEntity();
    }
}
