using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Renderer.Tests;

file sealed class OrdinaryGameplaySystem : EcsSystem
{
    public RendererSystem? Renderer;
    public long FrameInFlightAsObserved { get; private set; } = -1;

    protected override void Execute(World world, Time time)
    {
        if (FrameInFlightAsObserved < 0)
            FrameInFlightAsObserved = Renderer!.FrameInFlight.CurrentFrame;
    }
}

[Trait("Category", "RequiresGpu")]
public class RendererSystemOrderingTests
{
    [Fact]
    public void RendererSystem_RunsAfterAnOrdinaryUntaggedSystem_EvenRegisteredAfterIt()
    {
        // AddSystemCore, not the generated AddSystem<T>() sugar: this test project
        // doesn't reference Wyrd.Ecs.Generators as an analyzer.
        //
        // Registered deliberately AFTER AddWindow/AddRenderer - the actual adversarial
        // order for this bug. Registering a gameplay system before AddRenderer (i.e.
        // calling AddRenderer last, the "conventional" order) already works by accident of
        // registration-order tie-break even without any fix, since exclusive-stage nodes
        // open trailing stages in registration order - that's precisely the "only works
        // because .AddRenderer() is conventionally called last" bug this mechanism exists
        // to fix, so a same-order test wouldn't catch a regression back to it. This test
        // only passes because AddRenderer() applies Phase.PostUpdate fluently (via
        // SystemRegistration.Phase(), not a class attribute).
        var probe = new OrdinaryGameplaySystem();
        var builder = new WorldBuilder();
        builder
            .AddWindow("Renderer Ordering Test Window", 320, 240, SDL.WindowFlags.Hidden | SDL.WindowFlags.Vulkan)
            .AddRenderer();
        builder.AddSystemCore(
            typeof(OrdinaryGameplaySystem),
            access: null,
            construct: _ => probe,
            generatedBeforeTargets: [],
            generatedAfterTargets: []);
        var world = builder.Build();
        probe.Renderer = world.GetSystem<RendererSystem>();

        world.Update(TimeSpan.FromMilliseconds(16));

        // FrameInFlight.CurrentFrame increments once per RendererSystem.Execute (confirmed
        // by the existing Update_AdvancesTheFrameInFlightCounter test). If RendererSystem
        // had run before OrdinaryGameplaySystem this tick despite the adversarial
        // registration order, the probe would observe it already incremented (1), not the
        // pre-increment value (0).
        probe.FrameInFlightAsObserved.Should().Be(0);
        world.GetSystem<RendererSystem>().FrameInFlight.CurrentFrame.Should().Be(1);
    }
}
