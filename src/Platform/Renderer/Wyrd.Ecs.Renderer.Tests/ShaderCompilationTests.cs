namespace Wyrd.Ecs.Renderer.Tests;

public class ShaderCompilationTests
{
    [Theory]
    [InlineData("Wyrd.Ecs.Renderer.Shaders.UnlitSprite.vert.spirv")]
    [InlineData("Wyrd.Ecs.Renderer.Shaders.UnlitSprite.frag.spirv")]
    [InlineData("Wyrd.Ecs.Renderer.Shaders.UnlitSprite.vert.dxil")]
    [InlineData("Wyrd.Ecs.Renderer.Shaders.UnlitSprite.frag.dxil")]
    [InlineData("Wyrd.Ecs.Renderer.Shaders.UnlitSprite.vert.msl")]
    [InlineData("Wyrd.Ecs.Renderer.Shaders.UnlitSprite.frag.msl")]
    public void EmbeddedShaderResource_Exists_AndIsNonEmpty(string resourceName)
    {
        using var stream = typeof(RendererSystem).Assembly.GetManifestResourceStream(resourceName);

        stream.Should().NotBeNull();
        stream!.Length.Should().BeGreaterThan(0);
    }
}
