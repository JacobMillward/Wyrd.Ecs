using Wyrd.Ecs.Debug.Internal;

namespace Wyrd.Ecs.Debug.Tests.Internal;

public class PlaybackControlsTests
{
    [Fact]
    public void Pause_SetsIsPausedAndRaisesChanged()
    {
        var world = new World();
        var controls = new PlaybackControls(world);
        var raised = false;
        controls.Changed += () => raised = true;

        controls.Pause();

        controls.IsPaused.Should().BeTrue();
        raised.Should().BeTrue();
    }

    [Fact]
    public void Resume_AfterPause_ClearsIsPaused()
    {
        var world = new World();
        var controls = new PlaybackControls(world);
        controls.Pause();

        controls.Resume();

        controls.IsPaused.Should().BeFalse();
    }

    [Fact]
    public void SetTimeScale_UpdatesTimeScaleAndRaisesChanged()
    {
        var world = new World();
        var controls = new PlaybackControls(world);
        var raised = false;
        controls.Changed += () => raised = true;

        controls.SetTimeScale(2.5);

        controls.TimeScale.Should().Be(2.5);
        raised.Should().BeTrue();
    }
}
