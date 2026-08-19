using System.Numerics;

namespace Wyrd.Ecs.Renderer.Tests;

public class MeshBoundsTests
{
    private static readonly MeshVertex[] UnitCubeVertices =
    [
        new(new Vector3(-1, -1, -1), Vector3.UnitZ, Vector2.Zero),
        new(new Vector3(1, -1, -1), Vector3.UnitZ, Vector2.Zero),
        new(new Vector3(1, 1, -1), Vector3.UnitZ, Vector2.Zero),
        new(new Vector3(-1, 1, -1), Vector3.UnitZ, Vector2.Zero),
        new(new Vector3(-1, -1, 1), Vector3.UnitZ, Vector2.Zero),
        new(new Vector3(1, -1, 1), Vector3.UnitZ, Vector2.Zero),
        new(new Vector3(1, 1, 1), Vector3.UnitZ, Vector2.Zero),
        new(new Vector3(-1, 1, 1), Vector3.UnitZ, Vector2.Zero),
    ];

    [Fact]
    public void ComputeLocal_UnitCube_CentersAtOrigin()
    {
        var bounds = MeshBounds.ComputeLocal(UnitCubeVertices);

        bounds.Center.Should().Be(Vector3.Zero);
    }

    [Fact]
    public void ComputeLocal_UnitCube_RadiusCoversAllCorners()
    {
        var bounds = MeshBounds.ComputeLocal(UnitCubeVertices);

        var expectedRadius = Vector3.Distance(new Vector3(-1, -1, -1), new Vector3(1, 1, 1)) * 0.5f;
        bounds.Radius.Should().BeApproximately(expectedRadius, 1e-5f);
    }

    [Fact]
    public void ComputeWorld_ScaledTransform_RadiusScalesWithMaxAxis()
    {
        var local = new BoundingSphere(Vector3.Zero, 1f);
        var transform = new WorldTransform(Vector3.Zero, Quaternion.Identity, new Vector3(2f, 3f, 1f));

        var world = MeshBounds.ComputeWorld(transform, local);

        world.Radius.Should().Be(3f);
    }

    [Fact]
    public void ComputeWorld_TranslatedTransform_CenterMovesWithPosition()
    {
        var local = new BoundingSphere(Vector3.Zero, 1f);
        var transform = new WorldTransform(new Vector3(5, 0, 0), Quaternion.Identity, Vector3.One);

        var world = MeshBounds.ComputeWorld(transform, local);

        world.Center.Should().Be(new Vector3(5, 0, 0));
    }
}
