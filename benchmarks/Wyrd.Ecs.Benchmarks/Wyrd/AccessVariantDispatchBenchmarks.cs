using BenchmarkDotNet.Attributes;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

public struct AvdPosition : IComponent { public float X, Y; }
public struct AvdInterceptedPosition : IComponent { public float X, Y; }
public struct AvdInterceptedVelocity : IComponent { public float X, Y; }

/// <summary>
/// Measures whether routing through the canonical all-ref overload and, for a colliding
/// read-only call site, an interceptor costs anything versus today's direct dispatch.
/// All three cases are expected within noise of each other: reflection already confirmed
/// no wrapper thunk exists for the in-to-ref conversion (see the query-shape-access-
/// variants design spec's Risks section), this benchmark is the runtime-behavior half of
/// that claim reflection alone can't cover (JIT inlining/devirtualization).
///
/// Under the shipped design, a non-colliding shape's single real variant *is* its own
/// canonical overload -- no interceptor at all. So the intercepted benchmarks below use
/// their own dedicated component types (AvdInterceptedPosition/Velocity), each also
/// touched once by a `ref`-writing call site in <see cref="ForceCollision"/> -- purely to
/// force a genuine ref/in collision for that shape, so the interceptor path this
/// benchmark exists to measure is actually exercised, not silently bypassed.
/// </summary>
[MemoryDiagnoser]
public class AccessVariantDispatchBenchmarks
{
    public const int EntityCount = 20_000;
    private const int ResolutionsPerInvocation = 1024;

    private World _world = null!;
    private int _sink;

    [GlobalSetup]
    public void Setup()
    {
        _world = new World();
        for (var i = 0; i < EntityCount; i++)
            _world.Commands.CreateEntity(new AvdPosition(), new AvdInterceptedPosition(), new AvdInterceptedVelocity());
        _world.ApplyCommands();
        _world.AdvanceTick();

        ForceCollision();
    }

    /// <summary>
    /// Never benchmarked, called once from Setup: writes AvdInterceptedPosition/Velocity
    /// via `ref`, so those shapes have a genuine second access variant besides the `in`
    /// forms the benchmarks below read them with -- forcing both onto the canonical
    /// all-ref overload plus interceptor path this benchmark measures.
    /// </summary>
    private void ForceCollision()
    {
        _world.Query().With<AvdInterceptedPosition>().ForEach(0, (in int _, ref AvdInterceptedPosition p) => { p.X += 0f; });
        _world.Query().With<AvdInterceptedPosition>().With<AvdInterceptedVelocity>()
            .ForEach(0, (in int _, ref AvdInterceptedPosition p, ref AvdInterceptedVelocity v) => { p.X += v.Y * 0f; });
    }

    /// <summary>Baseline: single-variant ref-only shape, matches today's dispatch exactly (no collision, no interceptor).</summary>
    [Benchmark(Baseline = true)]
    public void Ref_SingleComponent()
    {
        for (var i = 0; i < ResolutionsPerInvocation; i++)
            _world.Query().With<AvdPosition>().ForEach(0, (in int _, ref AvdPosition p) => { _sink += p.Y > 0 ? 1 : 0; });
    }

    /// <summary>Colliding in-only call site, routed through the interceptor to the Ref backend.</summary>
    [Benchmark]
    public void In_SingleComponent_Intercepted()
    {
        for (var i = 0; i < ResolutionsPerInvocation; i++)
            _world.Query().With<AvdInterceptedPosition>().ForEach(0, (in int _, in AvdInterceptedPosition p) => { _sink += p.Y > 0 ? 1 : 0; });
    }

    /// <summary>Colliding mixed two-component shape (ref one component, in the other), the case Task 6 also covers for correctness.</summary>
    [Benchmark]
    public void Mixed_TwoComponent_Intercepted()
    {
        for (var i = 0; i < ResolutionsPerInvocation; i++)
            _world.Query().With<AvdInterceptedPosition>().With<AvdInterceptedVelocity>()
                .ForEach(0, (in int _, ref AvdInterceptedPosition p, in AvdInterceptedVelocity v) => { _sink += p.Y + v.Y > 0 ? 1 : 0; });
    }
}
