using System.Numerics;

namespace Wyrd.Ecs.Renderer.Tests;

public class SpriteBoundsTests
{
    [Fact]
    public void Compute_UnitScaleWholeTexture_RadiusMatchesHalfDiagonalInWorldUnits()
    {
        var transform = new WorldTransform(Vector3.Zero, Quaternion.Identity, Vector3.One);
        var sprite = new Sprite(SourceRect: null, Tint: Color.White);

        var bounds = SpriteBounds.Compute(transform, sprite, texturePixelWidth: 100, texturePixelHeight: 100);

        bounds.Center.Should().Be(Vector3.Zero);
        bounds.Radius.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void IsInsideFrustum_SphereAtOrigin_OrthographicCameraLookingAtIt_ReturnsTrue()
    {
        var camera = new OrthographicCamera(0, true, Size: 10f, Near: 0.1f, Far: 100f);
        var cameraTransform = new WorldTransform(new Vector3(0, 0, -5), Quaternion.Identity, Vector3.One);
        var viewProjection = camera.GetViewMatrix(cameraTransform) * camera.GetProjectionMatrix(1f);
        var bounds = new BoundingSphere(Vector3.Zero, 0.5f);

        FrustumCulling.IsInsideFrustum(bounds, viewProjection).Should().BeTrue();
    }

    [Fact]
    public void IsInsideFrustum_SphereFarOutsideFrustum_ReturnsFalse()
    {
        var camera = new OrthographicCamera(0, true, Size: 10f, Near: 0.1f, Far: 100f);
        var cameraTransform = new WorldTransform(new Vector3(0, 0, -5), Quaternion.Identity, Vector3.One);
        var viewProjection = camera.GetViewMatrix(cameraTransform) * camera.GetProjectionMatrix(1f);
        var bounds = new BoundingSphere(new Vector3(10_000, 0, 0), 0.5f);

        FrustumCulling.IsInsideFrustum(bounds, viewProjection).Should().BeFalse();
    }
}
