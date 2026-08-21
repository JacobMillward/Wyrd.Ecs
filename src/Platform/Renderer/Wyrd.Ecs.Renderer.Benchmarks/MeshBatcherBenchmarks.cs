using BenchmarkDotNet.Attributes;
using Wyrd.Ecs.Assets;

namespace Wyrd.Ecs.Renderer.Benchmarks;

/// <summary>Measures <see cref="MeshBatcher.Batch"/>'s steady-state cost, mirroring <see cref="SpriteBatcherBenchmarks"/>. Uses a handful of distinct meshes under one material, not one-per-entity, to exercise realistic grouping.</summary>
[MemoryDiagnoser]
public class MeshBatcherBenchmarks
{
    [Params(1_000, 20_000)]
    public int EntityCount { get; set; }

    private MeshBatcher _batcher = null!;
    private (Entity Entity, Material Material, Handle<Mesh> Mesh)[] _survivors = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var material = new Material(ShaderKind.UnlitMesh, Texture: null);
        Handle<Mesh>[] meshes = [new(0, 1), new(1, 1), new(2, 1), new(3, 1)];

        _survivors = new (Entity, Material, Handle<Mesh>)[EntityCount];
        for (var i = 0; i < EntityCount; i++)
            _survivors[i] = (new Entity(i + 1, 1), material, meshes[i % meshes.Length]);

        _batcher = new MeshBatcher();
        for (var warmup = 0; warmup < 5; warmup++)
            _batcher.Batch(_survivors);
    }

    [Benchmark]
    public void Batch_SteadyState() => _batcher.Batch(_survivors);
}
