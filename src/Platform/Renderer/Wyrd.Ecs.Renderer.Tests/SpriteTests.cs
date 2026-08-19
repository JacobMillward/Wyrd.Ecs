namespace Wyrd.Ecs.Renderer.Tests;

public class SpriteTests
{
    [Fact]
    public void Default_HasNoSourceRectAndWhiteTint()
    {
        var sprite = new Sprite(SourceRect: null, Tint: Color.White);

        sprite.SourceRect.Should().BeNull();
        sprite.Tint.Should().Be(Color.White);
    }
}
