using Wyrd.Ecs.Assets;

namespace Wyrd.Ecs.Renderer.Tests;

public class MaterialTests
{
    [Fact]
    public void TwoMaterialsWithSameShaderKindAndTexture_AreEqual()
    {
        var texture = new Handle<Texture>(1, 1);
        var a = new Material(ShaderKind.UnlitSprite, texture);
        var b = new Material(ShaderKind.UnlitSprite, texture);

        a.Should().Be(b);
    }

    [Fact]
    public void Material_DefaultsToOpaqueBlendMode()
    {
        var material = new Material(ShaderKind.UnlitSprite, new Handle<Texture>(1, 1));

        material.BlendMode.Should().Be(BlendMode.Opaque);
    }

    [Fact]
    public void TwoMaterialsWithDifferentBlendMode_AreNotEqual()
    {
        var texture = new Handle<Texture>(1, 1);
        var a = new Material(ShaderKind.UnlitSprite, texture, BlendMode.Opaque);
        var b = new Material(ShaderKind.UnlitSprite, texture, BlendMode.Transparent);

        a.Should().NotBe(b);
    }
}
