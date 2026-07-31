using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;
using Comparison.Wyrd;

namespace Comparison.EntityLifecycle;

public partial class EntityLifecycleBenchmarks
{
    private sealed class WyrdContext
    {
        public readonly World World = new();

        /// <summary>Reused scratch space for <see cref="Wyrd_DisposeEntity"/> — sized once, never reallocated, so it doesn't contaminate that method's own allocation measurement.</summary>
        public readonly Entity[] DisposeScratch = new Entity[EntityCount];
    }

    [Context] private WyrdContext _wyrd = null!;

    /// <summary>
    /// Rebuilds <see cref="_wyrd"/> fresh every iteration, scoped (via <c>Targets</c>) to
    /// only the four <c>Wyrd_Create*</c> methods below — <see cref="Wyrd_DisposeEntity"/>
    /// already destroys everything it creates each invocation, so its own
    /// <see cref="WyrdContext"/> never grows and needs no reset. The <c>Create*</c> methods
    /// do grow it every invocation, and since <c>[SimpleJob(invocationCount: 1)]</c> means
    /// each iteration is exactly one <see cref="EntityCount"/>-sized batch (hundreds of
    /// microseconds), that batch's real work vastly dominates this reset's cost (~200ns) —
    /// unlike the single-entity case this whole suite moved away from, IterationSetup
    /// doesn't spoil anything here. Without this reset the same context keeps growing across
    /// every iteration BenchmarkDotNet decides to run, so later iterations measure entity
    /// creation into a much bigger, previously-grown world than earlier ones — a
    /// non-stationary measurement, visible as huge outlier ranges and internally
    /// inconsistent numbers (confirmed: an actual run without this had
    /// <c>Wyrd_CreateFourComponentEntity</c> reporting as cheaper than
    /// <c>Wyrd_CreateOneComponentEntity</c>).
    /// </summary>
    [IterationSetup(Targets = [
        nameof(Wyrd_CreateBareEntity), nameof(Wyrd_CreateOneComponentEntity),
        nameof(Wyrd_CreateFourComponentEntity), nameof(Wyrd_CreateEightComponentEntity),
        nameof(Wyrd_CreateOneComponentEntity_Batch)])]
    public void Wyrd_ResetContext() => _wyrd = new WyrdContext();

    [Benchmark(Baseline = true, OperationsPerInvoke = EntityCount)]
    public void Wyrd_CreateBareEntity()
    {
        for (var i = 0; i < EntityCount; i++)
            _wyrd.World.Commands.CreateEntity();
        _wyrd.World.ApplyCommands();
    }

    [Benchmark(OperationsPerInvoke = EntityCount)]
    public void Wyrd_CreateOneComponentEntity()
    {
        for (var i = 0; i < EntityCount; i++)
            _wyrd.World.Commands.CreateEntity(new Position());
        _wyrd.World.ApplyCommands();
    }

    [Benchmark(OperationsPerInvoke = EntityCount)]
    public void Wyrd_CreateFourComponentEntity()
    {
        for (var i = 0; i < EntityCount; i++)
            _wyrd.World.Commands.CreateEntity(new Position(), new Velocity(), new Health(), new BulkPayload());
        _wyrd.World.ApplyCommands();
    }

    [Benchmark(OperationsPerInvoke = EntityCount)]
    public void Wyrd_CreateEightComponentEntity()
    {
        for (var i = 0; i < EntityCount; i++)
            _wyrd.World.Commands.CreateEntity(
                new Position(), new Velocity(), new Health(), new BulkPayload(),
                new Padding1(), new Padding2(), new Padding3(), new Padding4());
        _wyrd.World.ApplyCommands();
    }

    /// <summary>One <see cref="CommandBuffer.CreateEntity{T0}(int, T0)"/> call for the whole <see cref="EntityCount"/>-sized batch.</summary>
    [Benchmark(OperationsPerInvoke = EntityCount)]
    public void Wyrd_CreateOneComponentEntity_Batch()
    {
        _wyrd.World.Commands.CreateEntity(EntityCount, new Position());
        _wyrd.World.ApplyCommands();
    }

    /// <summary>
    /// Create-then-destroy, one batch of <see cref="EntityCount"/> pairs per invocation —
    /// not a single pre-seeded entity destroyed once, because <see cref="EntityLifecycleBenchmarks"/>
    /// builds <see cref="WyrdContext"/> once via <c>[GlobalSetup]</c> and reuses it across
    /// every invocation (see that class's docs): a single fixed target would only be alive
    /// for the first call, then destroy every call after that as a no-op. Pairing keeps
    /// every invocation self-resetting without needing a per-iteration reset.
    /// </summary>
    [Benchmark(OperationsPerInvoke = EntityCount)]
    public void Wyrd_DisposeEntity()
    {
        for (var i = 0; i < EntityCount; i++)
            _wyrd.DisposeScratch[i] = _wyrd.World.Commands.CreateEntity().Entity;
        _wyrd.World.ApplyCommands();

        for (var i = 0; i < EntityCount; i++)
            _wyrd.World.Commands.DestroyEntity(_wyrd.DisposeScratch[i]);
        _wyrd.World.ApplyCommands();
    }
}
