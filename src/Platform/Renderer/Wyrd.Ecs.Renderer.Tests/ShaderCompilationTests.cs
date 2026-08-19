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

    [Theory]
    [InlineData("Wyrd.Ecs.Renderer.Shaders.UnlitMesh.vert.spirv")]
    [InlineData("Wyrd.Ecs.Renderer.Shaders.UnlitMesh.frag.spirv")]
    [InlineData("Wyrd.Ecs.Renderer.Shaders.UnlitMesh.vert.dxil")]
    [InlineData("Wyrd.Ecs.Renderer.Shaders.UnlitMesh.frag.dxil")]
    [InlineData("Wyrd.Ecs.Renderer.Shaders.UnlitMesh.vert.msl")]
    [InlineData("Wyrd.Ecs.Renderer.Shaders.UnlitMesh.frag.msl")]
    public void EmbeddedShaderResource_Exists_AndIsNonEmpty_UnlitMesh(string resourceName)
    {
        using var stream = typeof(RendererSystem).Assembly.GetManifestResourceStream(resourceName);

        stream.Should().NotBeNull();
        stream!.Length.Should().BeGreaterThan(0);
    }
}
