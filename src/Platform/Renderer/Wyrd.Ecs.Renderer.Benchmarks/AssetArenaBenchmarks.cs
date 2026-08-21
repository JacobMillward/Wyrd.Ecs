using BenchmarkDotNet.Attributes;

namespace Wyrd.Ecs.Renderer.Benchmarks;

/// <summary>
/// Measures the two arena operations the Assets migration must not regress: <see
/// cref="TextureArena.TryGetTexture"/>/<see cref="TextureArena.GetState"/> (the per-frame,
/// per-visible-instance resolve call — see <c>RendererSystem.Sprites.cs:134</c>) and repeated <see
/// cref="TextureArena.Reserve"/> against an already-loaded path (the per-tick dedup-load call a
/// <c>[Resource] AssetLoader</c> consumer may make every tick). Run against this concrete,
/// pre-migration arena first to record a baseline; after migrating to <c>AssetArena&lt;string,
/// Texture&gt;</c>, this same benchmark (updated to the new type) must be within 5% on the first
/// case and no worse on the second — see the plan's Task 9.
/// </summary>
[MemoryDiagnoser]
public class AssetArenaBenchmarks
{
    [Params(1_000, 20_000)]
    public int InstanceCount { get; set; }

    private TextureArena _arena = null!;
    private Handle<Texture>[] _handles = null!;

    [GlobalSetup]
    public void Setup()
    {
        _arena = new TextureArena();
        _handles = new Handle<Texture>[InstanceCount];
        for (var i = 0; i < InstanceCount; i++)
        {
            var handle = _arena.Reserve($"texture-{i}.png");
            _arena.MarkLoaded(handle, new Texture(IntPtr.Zero, 1, 1));
            _handles[i] = handle;
        }
    }

    [Benchmark]
    public void ResolveVisibleInstances()
    {
        for (var i = 0; i < _handles.Length; i++)
        {
            _ = _arena.GetState(_handles[i]) == LoadState.Loaded ? _arena.TryGetTexture(_handles[i]) : null;
        }
    }

    [Benchmark]
    public void ReserveAlreadyLoadedPath()
    {
        for (var i = 0; i < _handles.Length; i++)
        {
            _arena.Reserve($"texture-{i}.png");
        }
    }
}
