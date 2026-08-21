namespace Wyrd.Ecs.Assets.Tests;

public class HandleTests
{
    private struct Texture;

    [Fact]
    public void Equals_SameIndexAndGeneration_AreEqual()
    {
        var a = new Handle<Texture>(3, 1);
        var b = new Handle<Texture>(3, 1);

        a.Should().Be(b);
    }

    [Fact]
    public void Equals_DifferentGeneration_AreNotEqual()
    {
        var a = new Handle<Texture>(3, 1);
        var b = new Handle<Texture>(3, 2);

        a.Should().NotBe(b);
    }
}
