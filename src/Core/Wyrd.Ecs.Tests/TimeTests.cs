namespace Wyrd.Ecs.Tests;

public class TimeTests
{
    [Fact]
    public void Time_ExposesDeltaAndElapsed()
    {
        var time = new Time(TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(10));

        time.Delta.Should().Be(TimeSpan.FromSeconds(0.5));
        time.Elapsed.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Time_HasValueEquality()
    {
        var a = new Time(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
        var b = new Time(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));

        a.Should().Be(b);
    }
}
