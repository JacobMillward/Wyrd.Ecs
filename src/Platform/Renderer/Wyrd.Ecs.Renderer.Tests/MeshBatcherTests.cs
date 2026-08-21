using Wyrd.Ecs.Assets;

namespace Wyrd.Ecs.Renderer.Tests;

public class MeshBatcherTests
{
    [Fact]
    public void Batch_SameMaterialDifferentMesh_ProducesSeparateBatches()
    {
        var batcher = new MeshBatcher();
        var material = new Material(ShaderKind.UnlitMesh, Texture: null);
        var meshA = new Handle<Mesh>(0, 0);
        var meshB = new Handle<Mesh>(1, 0);
        var entityA = new Entity(1, 1);
        var entityB = new Entity(2, 1);

        var batches = batcher.Batch([(entityA, material, meshA), (entityB, material, meshB)]);

        batches.Should().HaveCount(2);
    }

    [Fact]
    public void Batch_SameMaterialSameMesh_GroupsIntoOneBatch()
    {
        var batcher = new MeshBatcher();
        var material = new Material(ShaderKind.UnlitMesh, Texture: null);
        var mesh = new Handle<Mesh>(0, 0);
        var entityA = new Entity(1, 1);
        var entityB = new Entity(2, 1);

        var batches = batcher.Batch([(entityA, material, mesh), (entityB, material, mesh)]);

        batches.Should().ContainSingle();
        batches[0].Entities.Should().HaveCount(2);
    }

    [Fact]
    public void Batch_CalledTwice_ReusesStorageWithoutStaleEntries()
    {
        var batcher = new MeshBatcher();
        var material = new Material(ShaderKind.UnlitMesh, Texture: null);
        var mesh = new Handle<Mesh>(0, 0);
        var entity = new Entity(1, 1);
        batcher.Batch([(entity, material, mesh)]);

        var second = batcher.Batch([]);

        second.Should().BeEmpty();
    }
}
