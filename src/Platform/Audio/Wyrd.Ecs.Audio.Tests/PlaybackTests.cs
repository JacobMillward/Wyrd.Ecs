namespace Wyrd.Ecs.Audio.Tests;

public class PlaybackTests
{
    [Fact]
    public void Equals_SameIndexAndGeneration_AreEqual()
    {
        var a = new Playback(3, 1);
        var b = new Playback(3, 1);

        a.Should().Be(b);
    }

    [Fact]
    public void Equals_DifferentGeneration_AreNotEqual()
    {
        var a = new Playback(3, 1);
        var b = new Playback(3, 2);

        a.Should().NotBe(b);
    }
}
