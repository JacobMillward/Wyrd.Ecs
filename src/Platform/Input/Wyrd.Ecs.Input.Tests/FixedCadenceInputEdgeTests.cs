using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Input.Tests;

public class FixedCadenceInputEdgeTests
{
    private sealed class RecordingFixedSystem : EcsSystem
    {
        public readonly List<bool> Observed = [];

        protected override void Execute(World world, Time time) =>
            Observed.Add(world.GetResource<IntentState<TestAction>>()[TestAction.Jump].TickJustPressed);
    }

    private static (World World, BindingTable<TestAction> Bindings, RecordingFixedSystem Recorder) BuildWorld(TimeSpan fixedStep, int maxSubstepsPerUpdate = 5)
    {
        var bindings = new BindingTable<TestAction>();
        var recorder = new RecordingFixedSystem();
        var world = new WorldBuilder()
            .AddWindow("Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .WithFixedTimestep(fixedStep, maxSubstepsPerUpdate)
            .AddInput(bindings)
            .Build();
        // Phase.Update (default): runs before IntentTickResetSystem's Phase.PostUpdate each fixed-step iteration.
        world.AddSystemCore(
            typeof(RecordingFixedSystem), access: null, construct: _ => recorder,
            generatedBeforeTargets: [], generatedAfterTargets: [],
            cadence: SystemCadence.Fixed);
        return (world, bindings, recorder);
    }

    [Fact]
    public void RealFrameRateFasterThanFixedStep_FixedSystemStillObservesTheEdge()
    {
        var (world, bindings, recorder) = BuildWorld(TimeSpan.FromSeconds(1.0 / 60.0));
        bindings.Bind(TestAction.Jump, SDL.Scancode.Space);
        var shortFrame = TimeSpan.FromMilliseconds(1);
        world.Update(shortFrame);

        var down = new SDL.Event { Type = (uint)SDL.EventType.KeyDown, Key = new SDL.KeyboardEvent { Type = SDL.EventType.KeyDown, Scancode = SDL.Scancode.Space, Down = true } };
        SDL.PushEvent(ref down);
        // 30ms of 1ms real frames comfortably crosses the ~16.7ms fixed step at least once.
        for (var i = 0; i < 30; i++) world.Update(shortFrame);

        recorder.Observed.Should().Contain(true, "the fixed-cadence system must see the key-down edge despite many real calls happening before any fixed step runs");
    }

    [Fact]
    public void CatchUpBurst_FiresOnlyOnTheFirstSubstep()
    {
        var (world, bindings, recorder) = BuildWorld(TimeSpan.FromSeconds(1.0 / 60.0), maxSubstepsPerUpdate: 5);
        bindings.Bind(TestAction.Jump, SDL.Scancode.Space);
        var down = new SDL.Event { Type = (uint)SDL.EventType.KeyDown, Key = new SDL.KeyboardEvent { Type = SDL.EventType.KeyDown, Scancode = SDL.Scancode.Space, Down = true } };
        SDL.PushEvent(ref down);
        // Lets IntentSystem's Variable pass process the event and set TickJustPressed. The
        // burst below's fixed loop runs before Variable within a call, so without this the
        // burst would see nothing set yet.
        world.Update(TimeSpan.Zero);

        // One real call, five fixed steps' worth of accumulated time in one go.
        world.Update(TimeSpan.FromSeconds(5.0 / 60.0));

        recorder.Observed.Should().HaveCount(5);
        recorder.Observed[0].Should().BeTrue("the first substep observes the edge IntentSystem raised before this call's fixed loop ran");
        recorder.Observed.Skip(1).Should().AllSatisfy(observed => observed.Should().BeFalse("IntentTickResetSystem cleared it after the first substep; later substeps in the same burst must not re-fire"));
    }
}
