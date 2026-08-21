using Wyrd.Ecs.Assets;

namespace Wyrd.Ecs.Renderer.Tests;

public class MeshRendererTests
{
    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var handle = new Handle<Mesh>(1, 0);
        var first = new MeshRenderer(handle, Color.White);
        var second = new MeshRenderer(handle, Color.White);

        first.Should().Be(second);
    }

    [Fact]
    public void ShaderKind_UnlitMesh_HasExpectedName()
    {
        ShaderKind.UnlitMesh.Name.Should().Be("UnlitMesh");
    }
}
