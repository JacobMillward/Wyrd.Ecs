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
    public void CameraBundle_Defaults_ProduceOrderZeroAndClearOnBeginTrue()
    {
        var world = new WorldBuilder().Build();

        var entity = world.Commands.CreateEntity().Add(new CameraBundle(ProjectionMode.Orthographic, 10f, 0.1f, 100f));
        world.ApplyCommands();

        var camera = world.GetComponent<Camera>(entity);
        camera.Order.Should().Be(0);
        camera.ClearOnBegin.Should().BeTrue();
        camera.FieldOfViewOrOrthographicSize.Should().Be(10f);
        camera.Near.Should().Be(0.1f);
        camera.Far.Should().Be(100f);
    }
}
