using BenchmarkDotNet.Attributes;
using Wyrd.Ecs.Renderer;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

/// <summary>
/// Measures <see cref="SpriteBatcher.Batch"/>'s steady-state cost. <see cref="SpriteBatcher"/>
/// reuses its internal grouping storage across calls specifically to stay allocation-free per
/// frame; <see cref="MemoryDiagnoserAttribute"/> here confirms that rather than leaving it
/// asserted only in a doc comment. <see cref="GlobalSetup"/> below runs several warmup calls
/// before BenchmarkDotNet's own measured iterations start, so the reported numbers reflect
/// steady state (once <c>_batcher</c>'s internal dictionary has discovered every distinct
/// <see cref="Material"/> key), not first-call cost.
/// </summary>
[MemoryDiagnoser]
public class SpriteBatcherBenchmarks
{
    [Params(1_000, 20_000)]
    public int EntityCount { get; set; }

    [Params(1, 50)]
    public int DistinctMaterialCount { get; set; }

    private SpriteBatcher _batcher = null!;
    private (Entity Entity, Material Material)[] _survivors = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var materials = new Material[DistinctMaterialCount];
        for (var m = 0; m < DistinctMaterialCount; m++)
            materials[m] = new Material(ShaderKind.UnlitSprite, new Handle<Texture>(m, 1));

        _survivors = new (Entity, Material)[EntityCount];
        for (var i = 0; i < EntityCount; i++)
            _survivors[i] = (new Entity(i + 1, 1), materials[i % DistinctMaterialCount]);

        _batcher = new SpriteBatcher();
        for (var warmup = 0; warmup < 5; warmup++)
            _batcher.Batch(_survivors); // populate _batcher's internal dictionary before measurement starts
    }

    [Benchmark]
    public void Batch_SteadyState() => _batcher.Batch(_survivors);
}
