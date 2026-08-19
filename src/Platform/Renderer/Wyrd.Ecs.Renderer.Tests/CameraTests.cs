using System.Numerics;

namespace Wyrd.Ecs.Renderer.Tests;

public class CameraTests
{
    [Fact]
    public void ScreenToWorld_ThenWorldToScreen_OrthographicRoundTrips()
    {
        var camera = new Camera(0, ProjectionMode.Orthographic, true, FieldOfViewOrOrthographicSize: 10f, Near: 0.1f, Far: 100f);
        var transform = new WorldTransform(Vector3.Zero, Quaternion.Identity, Vector3.One);
        var viewport = new Vector2(800, 600);

        var world = camera.ScreenToWorld(transform, viewport.X / viewport.Y, viewport, new Vector3(400, 300, 0.5f));
        var screen = camera.WorldToScreen(transform, viewport.X / viewport.Y, viewport, world);

        screen.X.Should().BeApproximately(400f, 0.01f);
        screen.Y.Should().BeApproximately(300f, 0.01f);
    }

    [Fact]
    public void ScreenToWorld_TopLeftCorner_MapsToNegativeXPositiveY()
    {
        var camera = new Camera(0, ProjectionMode.Orthographic, true, FieldOfViewOrOrthographicSize: 10f, Near: 0.1f, Far: 100f);
        var transform = new WorldTransform(Vector3.Zero, Quaternion.Identity, Vector3.One);
        var viewport = new Vector2(800, 600);

        var world = camera.ScreenToWorld(transform, viewport.X / viewport.Y, viewport, new Vector3(0, 0, 0.5f));

        world.X.Should().BeNegative();
        world.Y.Should().BePositive();
    }
}
