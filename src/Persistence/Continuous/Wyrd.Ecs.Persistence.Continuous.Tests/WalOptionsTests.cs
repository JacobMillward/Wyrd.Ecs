namespace Wyrd.Ecs.Persistence.Continuous.Tests;

public class WalOptionsTests
{
    [Fact]
    public void Default_HasTheDocumentedDefaultValues()
    {
        var options = WalOptions.Default;

        options.FsyncInterval.Should().Be(TimeSpan.FromSeconds(1));
        options.CheckpointInterval.Should().Be(TimeSpan.FromSeconds(60));
        options.CheckpointThresholdBytes.Should().Be(64 * 1024 * 1024);
    }

    [Fact]
    public void Constructor_AllowsOverridingEachTunableIndependently()
    {
        var options = new WalOptions
        {
            FsyncInterval = TimeSpan.FromMilliseconds(500),
            CheckpointInterval = TimeSpan.FromSeconds(10),
            CheckpointThresholdBytes = 1024,
        };

        options.FsyncInterval.Should().Be(TimeSpan.FromMilliseconds(500));
        options.CheckpointInterval.Should().Be(TimeSpan.FromSeconds(10));
        options.CheckpointThresholdBytes.Should().Be(1024);
    }
}
