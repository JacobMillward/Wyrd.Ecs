using System.Collections.Generic;
using System.Linq;
using Wyrd.Ecs.Assets;

namespace Wyrd.Ecs.Renderer.Tests;

public class TransparentDrawableBatcherTests
{
    [Fact]
    public void Batch_TwoEntities_SortedFarthestFirst()
    {
        var key = new PipelineKey(ShaderKind.UnlitSprite, BlendMode.Transparent);
        var material = new Material(ShaderKind.UnlitSprite, new Handle<Texture>(1, 1), BlendMode.Transparent);
        var near = new Entity(1, 1);
        var far = new Entity(2, 1);

        var batcher = new TransparentDrawableBatcher();
        var batches = batcher.Batch(
        [
            (near, key, material, (Handle<Mesh>?)null, 1f),
            (far, key, material, (Handle<Mesh>?)null, 5f),
        ]);

        batches.Should().HaveCount(1);
        Slice(batcher, batches[0]).Should().Equal(far, near); // farthest (largest view-space Z) drawn first
    }

    [Fact]
    public void Batch_InputOrderDoesNotAffectSortedOutput()
    {
        var key = new PipelineKey(ShaderKind.UnlitSprite, BlendMode.Transparent);
        var material = new Material(ShaderKind.UnlitSprite, new Handle<Texture>(1, 1), BlendMode.Transparent);
        var near = new Entity(1, 1);
        var far = new Entity(2, 1);

        var batcher = new TransparentDrawableBatcher();
        var batches = batcher.Batch(
        [
            (far, key, material, (Handle<Mesh>?)null, 5f), // far listed first this time
            (near, key, material, (Handle<Mesh>?)null, 1f),
        ]);

        Slice(batcher, batches[0]).Should().Equal(far, near); // same output order regardless of input order
    }

    [Fact]
    public void Batch_DifferentKeyBetweenSameKeyEntries_SplitsIntoThreeBatches()
    {
        var spriteKey = new PipelineKey(ShaderKind.UnlitSprite, BlendMode.Transparent);
        var meshKey = new PipelineKey(ShaderKind.UnlitMesh, BlendMode.Transparent);
        var spriteMaterial = new Material(ShaderKind.UnlitSprite, new Handle<Texture>(1, 1), BlendMode.Transparent);
        var meshMaterial = new Material(ShaderKind.UnlitMesh, new Handle<Texture>(2, 1), BlendMode.Transparent);
        var mesh = new Handle<Mesh>(1, 1);
        var spriteFar = new Entity(1, 1);
        var meshMiddle = new Entity(2, 1);
        var spriteNear = new Entity(3, 1);

        var batcher = new TransparentDrawableBatcher();
        var batches = batcher.Batch(
        [
            (spriteFar, spriteKey, spriteMaterial, (Handle<Mesh>?)null, 10f),
            (meshMiddle, meshKey, meshMaterial, (Handle<Mesh>?)mesh, 5f),
            (spriteNear, spriteKey, spriteMaterial, (Handle<Mesh>?)null, 1f),
        ]);

        // Same sprite Material/key on both ends, but the mesh entry sorts between them in depth
        // order. It must not be merged into one sprite batch that skips over it: that would
        // draw the mesh either before or after both sprites, not between them.
        batches.Should().HaveCount(3);
        Slice(batcher, batches[0]).Should().Equal(spriteFar);
        Slice(batcher, batches[1]).Should().Equal(meshMiddle);
        Slice(batcher, batches[2]).Should().Equal(spriteNear);
    }

    [Fact]
    public void Batch_AdjacentSameKeyEntries_MergeIntoOneBatch()
    {
        var key = new PipelineKey(ShaderKind.UnlitSprite, BlendMode.Transparent);
        var material = new Material(ShaderKind.UnlitSprite, new Handle<Texture>(1, 1), BlendMode.Transparent);
        var a = new Entity(1, 1);
        var b = new Entity(2, 1);

        var batcher = new TransparentDrawableBatcher();
        var batches = batcher.Batch(
        [
            (a, key, material, (Handle<Mesh>?)null, 5f),
            (b, key, material, (Handle<Mesh>?)null, 4f),
        ]);

        batches.Should().ContainSingle();
        Slice(batcher, batches[0]).Should().Equal(a, b);
    }

    [Fact]
    public void Batch_CalledTwice_SecondCallDoesNotLeakFirstCallsEntities()
    {
        var key = new PipelineKey(ShaderKind.UnlitSprite, BlendMode.Transparent);
        var material = new Material(ShaderKind.UnlitSprite, new Handle<Texture>(1, 1), BlendMode.Transparent);
        var a = new Entity(1, 1);
        var b = new Entity(2, 1);
        var batcher = new TransparentDrawableBatcher();
        batcher.Batch([(a, key, material, (Handle<Mesh>?)null, 1f), (b, key, material, (Handle<Mesh>?)null, 2f)]);

        var second = batcher.Batch([(a, key, material, (Handle<Mesh>?)null, 1f)]);

        second.Should().ContainSingle();
        Slice(batcher, second[0]).Should().Equal(a);
    }

    /// <summary>Test-only helper. <see cref="TransparentBatch"/> carries a range into <see cref="TransparentDrawableBatcher.Entities"/> rather than its own list, so assertions need to slice it back out.</summary>
    private static IEnumerable<Entity> Slice(TransparentDrawableBatcher batcher, TransparentBatch batch) =>
        batcher.Entities.Skip(batch.EntityStart).Take(batch.EntityCount);
}
