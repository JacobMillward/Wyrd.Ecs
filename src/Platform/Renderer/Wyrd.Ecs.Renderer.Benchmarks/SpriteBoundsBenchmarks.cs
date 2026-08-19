using System.Numerics;
using BenchmarkDotNet.Attributes;

namespace Wyrd.Ecs.Renderer.Benchmarks;

/// <summary>
/// Measures the per-frame, per-camera cost of culling: <see cref="SpriteBounds.Compute"/> once
/// per sprite, <see cref="SpriteBounds.IsInsideFrustum"/> once per sprite per camera, at the
/// engine's ~20,000-entity target scale, matching <c>CheckpointBuildBenchmarks</c>'s own
/// <see cref="EntityCount"/> params.
/// </summary>
[MemoryDiagnoser]
public class SpriteBoundsBenchmarks
{
    [Params(1_000, 20_000)]
    public int EntityCount { get; set; }

    private WorldTransform[] _transforms = null!;
    private Sprite[] _sprites = null!;
    private Matrix4x4 _viewProjection;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _transforms = new WorldTransform[EntityCount];
        _sprites = new Sprite[EntityCount];
        var random = new Random(Seed: 1);
        for (var i = 0; i < EntityCount; i++)
        {
            _transforms[i] = new WorldTransform(new Vector3(random.Next(-500, 500), random.Next(-500, 500), 0), Quaternion.Identity, Vector3.One);
            _sprites[i] = new Sprite(SourceRect: null, Tint: Color.White);
        }

        var camera = new Camera(0, ProjectionMode.Orthographic, true, FieldOfViewOrOrthographicSize: 10f, Near: 0.1f, Far: 100f);
        var cameraTransform = new WorldTransform(Vector3.Zero, Quaternion.Identity, Vector3.One);
        _viewProjection = camera.GetViewMatrix(cameraTransform) * camera.GetProjectionMatrix(aspectRatio: 16f / 9f);
    }

    /// <summary>The exact per-sprite, per-frame cost <c>RendererSystem.DrawFrame</c> pays once per sprite, not per camera. See its <c>_spriteScratch</c> pass.</summary>
    [Benchmark]
    public void ComputeBoundsForAllSprites()
    {
        for (var i = 0; i < EntityCount; i++)
            SpriteBounds.Compute(_transforms[i], _sprites[i], texturePixelWidth: 32, texturePixelHeight: 32);
    }

    /// <summary>The exact per-sprite, per-camera cost. Runs once per active camera against every sprite's already-computed bounds.</summary>
    [Benchmark]
    public void CullAllSpritesAgainstOneCamera()
    {
        for (var i = 0; i < EntityCount; i++)
        {
            var bounds = SpriteBounds.Compute(_transforms[i], _sprites[i], texturePixelWidth: 32, texturePixelHeight: 32);
            FrustumCulling.IsInsideFrustum(bounds, _viewProjection);
        }
    }
}
