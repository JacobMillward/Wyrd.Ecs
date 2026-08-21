using Wyrd.Ecs.Assets;

namespace Wyrd.Ecs.Renderer.Tests;

public class SpriteBatcherTests
{
    [Fact]
    public void Batch_TwoEntitiesSameMaterial_OneBatchWithBoth()
    {
        var material = new Material(ShaderKind.UnlitSprite, new Handle<Texture>(1, 1));
        var a = new Entity(1, 1);
        var b = new Entity(2, 1);

        var batcher = new SpriteBatcher();
        var batches = batcher.Batch([(a, material), (b, material)]);

        batches.Should().HaveCount(1);
        batches[0].Entities.Should().BeEquivalentTo([a, b]);
    }

    [Fact]
    public void Batch_DifferentTextures_SeparateBatches()
    {
        var materialA = new Material(ShaderKind.UnlitSprite, new Handle<Texture>(1, 1));
        var materialB = new Material(ShaderKind.UnlitSprite, new Handle<Texture>(2, 1));
        var a = new Entity(1, 1);
        var b = new Entity(2, 1);

        var batcher = new SpriteBatcher();
        var batches = batcher.Batch([(a, materialA), (b, materialB)]);

        batches.Should().HaveCount(2);
    }

    [Fact]
    public void Batch_CalledTwiceWithDifferentSurvivors_SecondCallDoesNotLeakFirstCallsEntities()
    {
        var material = new Material(ShaderKind.UnlitSprite, new Handle<Texture>(1, 1));
        var a = new Entity(1, 1);
        var b = new Entity(2, 1);
        var batcher = new SpriteBatcher();

        batcher.Batch([(a, material), (b, material)]);
        var second = batcher.Batch([(a, material)]);

        second.Should().HaveCount(1);
        second[0].Entities.Should().BeEquivalentTo([a]);
    }
}
