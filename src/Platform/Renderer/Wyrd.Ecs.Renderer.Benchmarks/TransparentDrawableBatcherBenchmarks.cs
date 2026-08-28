using BenchmarkDotNet.Attributes;
using Wyrd.Ecs.Assets;

namespace Wyrd.Ecs.Renderer.Benchmarks;

/// <summary>
/// Measures <see cref="TransparentDrawableBatcher.Batch"/>'s steady-state cost. Sorting
/// dominates here (O(n log n)), unlike <see cref="SpriteBatcher"/>/<see cref="MeshBatcher"/>'s
/// pure O(n) grouping, so it's tracked separately. No target is set: this is a followup signal
/// only, per the design spec's "per-frame back-to-front sort cost" risk.
/// </summary>
[MemoryDiagnoser]
public class TransparentDrawableBatcherBenchmarks
{
    [Params(100, 2_000)]
    public int EntityCount { get; set; }

    private TransparentDrawableBatcher _batcher = null!;
    private (Entity Entity, PipelineKey PipelineKey, Material Material, Handle<Mesh>? Mesh, float ViewSpaceDepth)[] _survivors = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var key = new PipelineKey(ShaderKind.UnlitSprite, BlendMode.Transparent);
        var material = new Material(ShaderKind.UnlitSprite, new Handle<Texture>(1, 1), BlendMode.Transparent);

        _survivors = new (Entity, PipelineKey, Material, Handle<Mesh>?, float)[EntityCount];
        for (var i = 0; i < EntityCount; i++)
            _survivors[i] = (new Entity(i + 1, 1), key, material, null, EntityCount - i); // deliberately unsorted input

        _batcher = new TransparentDrawableBatcher();
        for (var warmup = 0; warmup < 5; warmup++)
            _batcher.Batch(_survivors);
    }

    [Benchmark]
    public void Batch_SteadyState() => _batcher.Batch(_survivors);
}
