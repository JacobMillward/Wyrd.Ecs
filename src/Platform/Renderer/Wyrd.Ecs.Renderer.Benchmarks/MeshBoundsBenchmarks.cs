using System.Numerics;
using BenchmarkDotNet.Attributes;

namespace Wyrd.Ecs.Renderer.Benchmarks;

/// <summary>Measures the per-frame, per-camera cost of mesh culling, mirroring <see cref="SpriteBoundsBenchmarks"/> at the same entity-count scale.</summary>
[MemoryDiagnoser]
public class MeshBoundsBenchmarks
{
    [Params(1_000, 20_000)]
    public int EntityCount { get; set; }

    private WorldTransform[] _transforms = null!;
    private BoundingSphere _localBounds;
    private Matrix4x4 _viewProjection;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _transforms = new WorldTransform[EntityCount];
        var random = new Random(Seed: 1);
        for (var i = 0; i < EntityCount; i++)
            _transforms[i] = new WorldTransform(new Vector3(random.Next(-500, 500), random.Next(-500, 500), random.Next(-500, 500)), Quaternion.Identity, Vector3.One);

        _localBounds = new BoundingSphere(Vector3.Zero, 1f);

        var camera = new PerspectiveCamera(0, true, FieldOfView: Angle.Rad(MathF.PI / 4f), Near: 0.1f, Far: 100f);
        var cameraTransform = new WorldTransform(new Vector3(0, 0, -5), Quaternion.Identity, Vector3.One);
        _viewProjection = camera.GetViewMatrix(cameraTransform) * camera.GetProjectionMatrix(aspectRatio: 16f / 9f);
    }

    [Benchmark]
    public void ComputeWorldBoundsForAllMeshes()
    {
        for (var i = 0; i < EntityCount; i++)
            MeshBounds.ComputeWorld(_transforms[i], _localBounds);
    }

    [Benchmark]
    public void CullAllMeshesAgainstOneCamera()
    {
        for (var i = 0; i < EntityCount; i++)
        {
            var bounds = MeshBounds.ComputeWorld(_transforms[i], _localBounds);
            FrustumCulling.IsInsideFrustum(bounds, _viewProjection);
        }
    }
}
