using System.Numerics;
using System.Text;
using Silk.NET.Assimp;

namespace Wyrd.Ecs.Renderer;

/// <summary>
/// Pure parsing: no SDL, no GPU device, unit-testable directly (matches this project's
/// existing split between logic with no GPU dependency and logic that needs a real device, see
/// the design spec's "Testing and CI"). Wraps Assimp via <see cref="Silk.NET.Assimp"/> rather
/// than a hand-rolled OBJ parser: the pure-managed OBJ-parser ecosystem is stale and
/// unmaintained, while Silk.NET.Assimp is actively maintained and its native package carries
/// explicit NativeAOT fixes. Every format Assimp itself reports supporting
/// (<see cref="IsExtensionSupported"/>) is usable, not just OBJ; there's no per-format registry,
/// since one Assimp-backed loader already covers 71 formats.
/// </summary>
internal static class MeshLoader
{
    private static readonly Assimp Api = Assimp.GetApi();

    /// <summary>One Assimp sub-mesh: real vertex/index data plus, if its material references one, the diffuse texture path resolved relative to the source file's own directory.</summary>
    public readonly record struct ParsedSubMesh(MeshVertex[] Vertices, uint[] Indices, string? TexturePath);

    /// <summary>
    /// Parses <paramref name="path"/> synchronously; callers wanting this off the calling
    /// thread wrap it in their own <c>Task.Run</c> (see <see cref="RendererSystem.LoadModel"/>).
    /// One <see cref="ParsedSubMesh"/> per Assimp <c>aiMesh</c>: Assimp itself splits a
    /// multi-material source file into one sub-mesh per <c>usemtl</c>/material group, confirmed
    /// hands-on rather than assumed. That's the exact per-part unit
    /// <see cref="RendererSystem.LoadModel"/> spawns one child entity for.
    /// </summary>
    public static unsafe IReadOnlyList<ParsedSubMesh> Load(string path)
    {
        var scene = Api.ImportFile(path, (uint)(PostProcessSteps.Triangulate | PostProcessSteps.JoinIdenticalVertices | PostProcessSteps.GenerateNormals));
        if (scene == null || (scene->MFlags & (uint)SceneFlags.Incomplete) != 0 || scene->MRootNode == null)
            throw new InvalidOperationException($"Assimp import of '{path}' failed: {Api.GetErrorStringS()}");

        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? "";
            var result = new List<ParsedSubMesh>((int)scene->MNumMeshes);

            for (var m = 0; m < scene->MNumMeshes; m++)
            {
                var mesh = scene->MMeshes[m];

                var vertices = new MeshVertex[mesh->MNumVertices];
                for (var v = 0; v < mesh->MNumVertices; v++)
                {
                    var position = mesh->MVertices[v];
                    var normal = mesh->MNormals != null ? mesh->MNormals[v] : default;
                    var uv = mesh->MTextureCoords[0] != null ? mesh->MTextureCoords[0][v] : default;
                    vertices[v] = new MeshVertex(
                        new Vector3(position.X, position.Y, position.Z),
                        new Vector3(normal.X, normal.Y, normal.Z),
                        new Vector2(uv.X, uv.Y));
                }

                var indices = new List<uint>((int)mesh->MNumFaces * 3);
                for (var f = 0; f < mesh->MNumFaces; f++)
                {
                    var face = mesh->MFaces[f];
                    for (var k = 0; k < face.MNumIndices; k++)
                        indices.Add(face.MIndices[k]);
                }

                string? texturePath = null;
                if (mesh->MMaterialIndex < scene->MNumMaterials)
                {
                    var material = scene->MMaterials[mesh->MMaterialIndex];
                    AssimpString rawPath = default;
                    if (Api.GetMaterialTexture(material, TextureType.Diffuse, 0, ref rawPath, null, null, null, null, null, null) == Return.ReturnSuccess)
                    {
                        var relative = Encoding.UTF8.GetString(rawPath.Data, (int)rawPath.Length);
                        texturePath = Path.Combine(directory, relative);
                    }
                }

                result.Add(new ParsedSubMesh(vertices, indices.ToArray(), texturePath));
            }

            return result;
        }
        finally
        {
            Api.ReleaseImport(scene);
        }
    }

    /// <summary>Backs <see cref="RendererSystem.LoadModel"/>'s "any format Assimp itself supports" contract.</summary>
    public static unsafe bool IsExtensionSupported(string path)
    {
        AssimpString extensionList = default;
        Api.GetExtensionList(ref extensionList);
        var extensions = Encoding.UTF8.GetString(extensionList.Data, (int)extensionList.Length);
        var extension = Path.GetExtension(path).TrimStart('.');
        return extensions.Split(';').Any(e => e.TrimStart('*', '.').Equals(extension, StringComparison.OrdinalIgnoreCase));
    }
}
