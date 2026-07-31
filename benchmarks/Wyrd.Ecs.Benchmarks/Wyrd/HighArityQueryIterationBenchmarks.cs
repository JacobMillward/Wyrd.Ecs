using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;
using Comparison.Wyrd;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

/// <summary>
/// Arity 8 and 12 query iteration — Wyrd.Ecs-only, since both Friflo
/// (<c>ArchetypeQuery&lt;T1..T5&gt;</c>) and fennecs (<c>Stream&lt;C0..C4&gt;</c>) are hard-capped
/// at arity 5. Demonstrates the arity cap the generator-backed unbounded query-shape redesign
/// removed from Wyrd.Ecs — see
/// docs/superpowers/specs/2026-07-25-generator-backed-unbounded-query-shape-design.md. Component
/// construction is one-at-a-time via <c>Commands.AddComponent</c>, not the batched
/// <c>CreateEntity{T...}</c> overload — that overload only goes up to 8 type arguments
/// (<c>QueryArity.Max</c>), which covers arity 8 but not arity 12; one-at-a-time construction
/// (matching <c>QueryArityBoundaryTests.cs</c>'s precedent) works uniformly for both.
/// </summary>
[MemoryDiagnoser]
public class HighArityQueryIterationBenchmarks
{
    private const int EntityCount = 10_000;

    [Params(false, true)]
    public bool Fragmented { get; set; }

    private static readonly ArchetypeQuery Query8 = ArchetypeQuery.Empty
        .Access<Ref<Position>>().Access<Ref<Velocity>>().Access<Mut<Health>>().Access<Ref<BulkPayload>>()
        .Access<Ref<Padding1>>().Access<Ref<Padding2>>().Access<Ref<Padding3>>().Access<Ref<Padding4>>();

    private static readonly ArchetypeQuery Query12 = ArchetypeQuery.Empty
        .Access<Ref<Position>>().Access<Ref<Velocity>>().Access<Mut<Health>>().Access<Ref<BulkPayload>>()
        .Access<Ref<Padding1>>().Access<Ref<Padding2>>().Access<Ref<Padding3>>().Access<Ref<Padding4>>()
        .Access<Ref<Padding5>>().Access<Ref<Padding6>>().Access<Ref<Padding7>>().Access<Ref<Padding8>>();

    private World _world8 = null!;
    private World _world12 = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _world8 = new World();
        _world12 = new World();

        for (var i = 0; i < EntityCount; i++)
        {
            var e8 = _world8.Commands.CreateEntity().Entity;
            _world8.Commands.AddComponent(e8, new Position());
            _world8.Commands.AddComponent(e8, new Velocity());
            _world8.Commands.AddComponent(e8, new Health());
            _world8.Commands.AddComponent(e8, new BulkPayload());
            _world8.Commands.AddComponent(e8, new Padding1());
            _world8.Commands.AddComponent(e8, new Padding2());
            _world8.Commands.AddComponent(e8, new Padding3());
            _world8.Commands.AddComponent(e8, new Padding4());

            var e12 = _world12.Commands.CreateEntity().Entity;
            _world12.Commands.AddComponent(e12, new Position());
            _world12.Commands.AddComponent(e12, new Velocity());
            _world12.Commands.AddComponent(e12, new Health());
            _world12.Commands.AddComponent(e12, new BulkPayload());
            _world12.Commands.AddComponent(e12, new Padding1());
            _world12.Commands.AddComponent(e12, new Padding2());
            _world12.Commands.AddComponent(e12, new Padding3());
            _world12.Commands.AddComponent(e12, new Padding4());
            _world12.Commands.AddComponent(e12, new Padding5());
            _world12.Commands.AddComponent(e12, new Padding6());
            _world12.Commands.AddComponent(e12, new Padding7());
            _world12.Commands.AddComponent(e12, new Padding8());

            if (Fragmented)
            {
                Fragmentation.AddFragTag(_world8, e8, i);
                Fragmentation.AddFragTag(_world12, e12, i);
            }
        }

        _world8.ApplyCommands();
        _world12.ApplyCommands();
    }

    [Benchmark(Baseline = true)]
    public void EightComponent_ArchetypeQuery()
    {
        foreach (var chunk in Query8.Resolve(_world8))
        {
            var position = chunk.Access<Ref<Position>>();
            var velocity = chunk.Access<Ref<Velocity>>();
            var health = chunk.Access<Mut<Health>>();
            var payload = chunk.Access<Ref<BulkPayload>>();
            var padding1 = chunk.Access<Ref<Padding1>>();
            var padding2 = chunk.Access<Ref<Padding2>>();
            var padding3 = chunk.Access<Ref<Padding3>>();
            var padding4 = chunk.Access<Ref<Padding4>>();
            for (var i = 0; i < chunk.Count; i++)
                health[i].Current += (position[i].X + velocity[i].X + payload[i].A
                    + padding1[i].Value + padding2[i].Value + padding3[i].Value + padding4[i].Value) * 0f;
        }
    }

    [Benchmark]
    public void EightComponent_FluentChain()
    {
        _world8.Query()
            .With<Position>().With<Velocity>().With<Health>().With<BulkPayload>()
            .With<Padding1>().With<Padding2>().With<Padding3>().With<Padding4>()
            .ForEach(0, (in int _, in Position p, in Velocity v, ref Health h, in BulkPayload b,
                in Padding1 pad1, in Padding2 pad2, in Padding3 pad3, in Padding4 pad4) =>
                h.Current += (p.X + v.X + b.A + pad1.Value + pad2.Value + pad3.Value + pad4.Value) * 0f);
    }

    [Benchmark]
    public void TwelveComponent_ArchetypeQuery()
    {
        foreach (var chunk in Query12.Resolve(_world12))
        {
            var position = chunk.Access<Ref<Position>>();
            var velocity = chunk.Access<Ref<Velocity>>();
            var health = chunk.Access<Mut<Health>>();
            var payload = chunk.Access<Ref<BulkPayload>>();
            var padding1 = chunk.Access<Ref<Padding1>>();
            var padding2 = chunk.Access<Ref<Padding2>>();
            var padding3 = chunk.Access<Ref<Padding3>>();
            var padding4 = chunk.Access<Ref<Padding4>>();
            var padding5 = chunk.Access<Ref<Padding5>>();
            var padding6 = chunk.Access<Ref<Padding6>>();
            var padding7 = chunk.Access<Ref<Padding7>>();
            var padding8 = chunk.Access<Ref<Padding8>>();
            for (var i = 0; i < chunk.Count; i++)
                health[i].Current += (position[i].X + velocity[i].X + payload[i].A
                    + padding1[i].Value + padding2[i].Value + padding3[i].Value + padding4[i].Value
                    + padding5[i].Value + padding6[i].Value + padding7[i].Value + padding8[i].Value) * 0f;
        }
    }

    [Benchmark]
    public void TwelveComponent_FluentChain()
    {
        _world12.Query()
            .With<Position>().With<Velocity>().With<Health>().With<BulkPayload>()
            .With<Padding1>().With<Padding2>().With<Padding3>().With<Padding4>()
            .With<Padding5>().With<Padding6>().With<Padding7>().With<Padding8>()
            .ForEach(0, (in int _, in Position p, in Velocity v, ref Health h, in BulkPayload b,
                in Padding1 pad1, in Padding2 pad2, in Padding3 pad3, in Padding4 pad4,
                in Padding5 pad5, in Padding6 pad6, in Padding7 pad7, in Padding8 pad8) =>
                h.Current += (p.X + v.X + b.A + pad1.Value + pad2.Value + pad3.Value + pad4.Value
                    + pad5.Value + pad6.Value + pad7.Value + pad8.Value) * 0f);
    }
}
