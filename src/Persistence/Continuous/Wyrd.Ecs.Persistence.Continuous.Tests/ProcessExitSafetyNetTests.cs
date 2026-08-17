using Wyrd.Ecs.Persistence.Continuous.Internal;

namespace Wyrd.Ecs.Persistence.Continuous.Tests;

/// <summary>
/// Groups every test touching <see cref="ProcessExitSafetyNet"/>'s process-wide session
/// table, since <see cref="ProcessExitSafetyNet.StopAllTrackedSessions"/> sweeps every
/// registered session in the process, not just the calling test's own World, and would
/// otherwise race a concurrently-scheduled test class with a live session.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ProcessExitSafetyNetCollection
{
    public const string Name = "ProcessExitSafetyNet (global static)";
}

[Collection(ProcessExitSafetyNetCollection.Name)]
public class ProcessExitSafetyNetTests
{
    [Fact]
    public void StopAllTrackedSessions_InvokesEveryRegisteredStopWithMergeTrue()
    {
        var calls = new List<bool>();
        var world = new World();
        ProcessExitSafetyNet.Register(world, merge => calls.Add(merge));

        ProcessExitSafetyNet.StopAllTrackedSessions();

        calls.Should().Equal(true);
    }

    [Fact]
    public void Unregister_PreventsAFutureSweepFromStoppingThatSession()
    {
        var calls = new List<bool>();
        var world = new World();
        ProcessExitSafetyNet.Register(world, merge => calls.Add(merge));

        ProcessExitSafetyNet.Unregister(world);
        ProcessExitSafetyNet.StopAllTrackedSessions();

        calls.Should().BeEmpty();
    }

    [Fact]
    public void StopAllTrackedSessions_WhenOneStopThrows_StillStopsTheOthers()
    {
        var secondCalled = false;
        var worldA = new World();
        var worldB = new World();
        ProcessExitSafetyNet.Register(worldA, _ => throw new InvalidOperationException("simulated"));
        ProcessExitSafetyNet.Register(worldB, _ => secondCalled = true);

        var act = () => ProcessExitSafetyNet.StopAllTrackedSessions();

        act.Should().NotThrow();
        secondCalled.Should().BeTrue();
    }

    [Fact]
    public void StopAllTrackedSessions_ClearsTheTrackedSetSoASecondSweepCallsNothing()
    {
        var calls = 0;
        var world = new World();
        ProcessExitSafetyNet.Register(world, _ => calls++);

        ProcessExitSafetyNet.StopAllTrackedSessions();
        ProcessExitSafetyNet.StopAllTrackedSessions();

        calls.Should().Be(1);
    }
}
