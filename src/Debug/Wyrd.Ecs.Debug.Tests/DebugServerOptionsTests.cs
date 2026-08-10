namespace Wyrd.Ecs.Debug.Tests;

public class DebugServerOptionsTests
{
    [Fact]
    public void Defaults_MatchTheDocumentedValues()
    {
        var options = new DebugServerOptions();

        options.Port.Should().Be(5299);
        options.ChangeLogCapacity.Should().Be(500);
        options.OnError.Should().BeNull();
    }
}
