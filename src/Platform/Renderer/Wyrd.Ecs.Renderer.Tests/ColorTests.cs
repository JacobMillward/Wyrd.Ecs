namespace Wyrd.Ecs.Renderer.Tests;

public class ColorTests
{
    [Fact]
    public void White_IsOpaqueWhite()
    {
        Color.White.Should().Be(new Color(1f, 1f, 1f, 1f));
    }
}
