namespace Wyrd.Ecs.Renderer.Tests;

public class MeshLoaderTests
{
    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public void Load_SingleMaterialCube_ReturnsOneSubMesh()
    {
        var parts = MeshLoader.Load(FixturePath("cube.obj"));

        parts.Should().ContainSingle();
    }

    [Fact]
    public void Load_SingleMaterialCube_VertexAndIndexCountsAreCorrect()
    {
        var parts = MeshLoader.Load(FixturePath("cube.obj"));

        // 2 quads, opposite face normals -> JoinIdenticalVertices can't merge across them (only
        // exact position+normal+uv matches merge), so all 8 corners stay distinct.
        parts[0].Vertices.Should().HaveCount(8);
        parts[0].Indices.Should().HaveCount(12);
    }

    [Fact]
    public void Load_MultiMaterialCube_ReturnsOneSubMeshPerMaterial()
    {
        var parts = MeshLoader.Load(FixturePath("cube-multimaterial.obj"));

        parts.Should().HaveCount(2);
    }

    [Fact]
    public void Load_MultiMaterialCube_ResolvesTexturePathsRelativeToSourceDirectory()
    {
        var parts = MeshLoader.Load(FixturePath("cube-multimaterial.obj"));

        var fixturesDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        parts.Select(p => p.TexturePath).Should().BeEquivalentTo(
        [
            Path.Combine(fixturesDir, "red.png"),
            Path.Combine(fixturesDir, "blue.png"),
        ]);
    }

    [Fact]
    public void Load_MissingFile_Throws()
    {
        var act = () => MeshLoader.Load(FixturePath("does-not-exist.obj"));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void IsExtensionSupported_Obj_ReturnsTrue()
    {
        MeshLoader.IsExtensionSupported("model.obj").Should().BeTrue();
    }

    [Fact]
    public void IsExtensionSupported_Gltf_ReturnsTrue()
    {
        MeshLoader.IsExtensionSupported("model.gltf").Should().BeTrue();
    }

    [Fact]
    public void IsExtensionSupported_UnknownExtension_ReturnsFalse()
    {
        MeshLoader.IsExtensionSupported("model.notarealformat").Should().BeFalse();
    }
}
