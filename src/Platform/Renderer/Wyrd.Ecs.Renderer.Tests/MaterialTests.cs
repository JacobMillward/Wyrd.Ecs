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
}
