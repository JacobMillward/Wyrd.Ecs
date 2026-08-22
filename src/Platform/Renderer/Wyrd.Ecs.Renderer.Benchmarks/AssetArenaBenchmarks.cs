using BenchmarkDotNet.Attributes;
using Wyrd.Ecs.Assets;

namespace Wyrd.Ecs.Renderer.Benchmarks;

/// <summary>
/// Measures the two arena operations the Assets migration must not regress: <see
/// cref="AssetArena{TKey,TAsset}.TryGet"/>/<see cref="AssetArena{TKey,TAsset}.GetState"/> (the
/// per-frame, per-visible-instance resolve call (see <c>RendererSystem.Sprites.cs</c>'s
/// <c>ResolveTexture</c>)) and repeated <see cref="AssetArena{TKey,TAsset}.Reserve"/> against an
/// already-loaded path (the per-tick dedup-load call a <c>[Resource] RenderAssets</c> consumer may
/// make every tick). Baseline recorded against the pre-migration concrete <c>TextureArena</c>
/// (mean ~13.7-14.8us / ~271-274us for ResolveVisibleInstances at 1,000/20,000 instances,
/// ~35.1-35.6us / ~827-839us for ReserveAlreadyLoadedPath); see the Task 5 commit. This version
/// targets the post-migration generic arena directly; compared against that baseline in Task 9.
/// </summary>
[MemoryDiagnoser]
public class AssetArenaBenchmarks
{
    [Params(1_000, 20_000)]
    public int InstanceCount { get; set; }

    private AssetArena<string, Texture> _arena = null!;
    private Handle<Texture>[] _handles = null!;

    [GlobalSetup]
    public void Setup()
    {
        _arena = new AssetArena<string, Texture>();
        _handles = new Handle<Texture>[InstanceCount];
        for (var i = 0; i < InstanceCount; i++)
        {
            var handle = _arena.Reserve($"texture-{i}.png", out _);
            _arena.MarkLoaded(handle, new Texture(IntPtr.Zero, 1, 1));
            _handles[i] = handle;
        }
    }

    [Benchmark]
    public int ResolveVisibleInstances()
    {
        // Same GetState-then-conditional-TryGet flow as ResolveTexture, with the resolved
        // texture counted so the JIT cannot eliminate either call.
        var resolved = 0;
        for (var i = 0; i < _handles.Length; i++)
        {
            if (_arena.GetState(_handles[i]) == LoadState.Loaded && _arena.TryGet(_handles[i]) is not null)
                resolved++;
        }

        return resolved;
    }

    [Benchmark]
    public void ReserveAlreadyLoadedPath()
    {
        for (var i = 0; i < _handles.Length; i++)
        {
            _arena.Reserve($"texture-{i}.png", out _);
        }
    }
}
