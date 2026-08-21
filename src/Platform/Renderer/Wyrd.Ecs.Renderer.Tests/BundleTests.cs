namespace Wyrd.Ecs.Renderer.Tests;

public class BundleTests
{
    [Fact]
    public void SpriteBundle_Defaults_ProduceWhiteTintAndUnlitSpriteShader()
    {
        var world = new WorldBuilder().Build();
        var texture = new Handle<Texture>(0, 0);

        var entity = world.Commands.CreateEntity().Add(new SpriteBundle(texture));
        world.ApplyCommands();

        world.GetComponent<Sprite>(entity).Tint.Should().Be(Color.White);
        world.GetComponent<Material>(entity).ShaderKind.Should().Be(ShaderKind.UnlitSprite);
        world.GetComponent<Material>(entity).Texture.Should().Be(texture);
    }

    [Fact]
    public void SpriteBundle_ExplicitArguments_OverrideDefaults()
    {
        var world = new WorldBuilder().Build();
        var texture = new Handle<Texture>(0, 0);
        var sourceRect = new Rect(0, 0, 16, 16);
        var customShader = new ShaderKind("Custom");
        var customTint = new Color(1f, 0f, 0f, 1f);

        var entity = world.Commands.CreateEntity().Add(new SpriteBundle(texture, sourceRect, customTint, customShader));
        world.ApplyCommands();

        world.GetComponent<Sprite>(entity).SourceRect.Should().Be(sourceRect);
        world.GetComponent<Sprite>(entity).Tint.Should().Be(customTint);
        world.GetComponent<Material>(entity).ShaderKind.Should().Be(customShader);
    }

    [Fact]
    public void MeshBundle_Defaults_ProduceWhiteTintAndUnlitMeshShader()
    {
        var world = new WorldBuilder().Build();
        var mesh = new Handle<Mesh>(0, 0);

        var entity = world.Commands.CreateEntity().Add(new MeshBundle(mesh));
        world.ApplyCommands();

        world.GetComponent<MeshRenderer>(entity).Tint.Should().Be(Color.White);
        world.GetComponent<Material>(entity).ShaderKind.Should().Be(ShaderKind.UnlitMesh);
    }

    [Fact]
    public void OrthographicCameraBundle_Defaults_ProduceOrderZeroAndClearOnBeginTrue()
    {
        var world = new WorldBuilder().Build();

        var entity = world.Commands.CreateEntity().Add(new OrthographicCameraBundle(Size: 10f, Near: 0.1f, Far: 100f));
        world.ApplyCommands();

        var camera = world.GetComponent<OrthographicCamera>(entity);
        camera.Order.Should().Be(0);
        camera.ClearOnBegin.Should().BeTrue();
        camera.Size.Should().Be(10f);
        camera.Near.Should().Be(0.1f);
        camera.Far.Should().Be(100f);
    }

    [Fact]
    public void OrthographicCameraBundle_ExplicitOrderAndClearOnBegin_OverrideDefaults()
    {
        var world = new WorldBuilder().Build();

        var entity = world.Commands.CreateEntity().Add(new OrthographicCameraBundle(Size: 10f, Near: 0.1f, Far: 100f, Order: 1, ClearOnBegin: false));
        world.ApplyCommands();

        var camera = world.GetComponent<OrthographicCamera>(entity);
        camera.Order.Should().Be(1);
        camera.ClearOnBegin.Should().BeFalse();
    }

    [Fact]
    public void PerspectiveCameraBundle_Defaults_ProduceOrderZeroClearOnBeginTrueAndConvertsAngleToRadians()
    {
        var world = new WorldBuilder().Build();
        var fov = Angle.Deg(60f);

        var entity = world.Commands.CreateEntity().Add(new PerspectiveCameraBundle(fov, 0.1f, 100f));
        world.ApplyCommands();

        var camera = world.GetComponent<PerspectiveCamera>(entity);
        camera.Order.Should().Be(0);
        camera.ClearOnBegin.Should().BeTrue();
        camera.FieldOfView.Radians.Should().BeApproximately(fov.Radians, 0.0001f);
        camera.Near.Should().Be(0.1f);
        camera.Far.Should().Be(100f);
    }

    [Fact]
    public void PerspectiveCameraBundle_ExplicitOrderAndClearOnBegin_OverrideDefaults()
    {
        var world = new WorldBuilder().Build();

        var entity = world.Commands.CreateEntity().Add(new PerspectiveCameraBundle(Angle.Deg(60f), 0.1f, 100f, Order: 1, ClearOnBegin: false));
        world.ApplyCommands();

        var camera = world.GetComponent<PerspectiveCamera>(entity);
        camera.Order.Should().Be(1);
        camera.ClearOnBegin.Should().BeFalse();
    }
}
