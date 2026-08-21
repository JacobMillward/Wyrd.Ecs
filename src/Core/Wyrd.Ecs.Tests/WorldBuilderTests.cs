namespace Wyrd.Ecs.Tests;

file sealed class CadenceProbeSystem : EcsSystem { protected override void Execute(World world, Time time) { } }

file sealed class DependencyRootSystem : EcsSystem { protected override void Execute(World world, Time time) { } }

file sealed class DependencyLeafSystem : EcsSystem
{
    public DependencyRootSystem Root { get; }
    public DependencyLeafSystem(World world) => Root = world.GetSystem<DependencyRootSystem>();
    protected override void Execute(World world, Time time) { }
}

file sealed class CycleASystem : EcsSystem { protected override void Execute(World world, Time time) { } }
file sealed class CycleBSystem : EcsSystem { protected override void Execute(World world, Time time) { } }

public class WorldBuilderTests
{
    private struct Position : IComponent
    {
        public float X;
    }

    [Fact]
    public void Build_ProducesAWorkingWorld()
    {
        var world = new WorldBuilder().Build();
        Entity entity = world.Commands.CreateEntity(new Position { X = 5f });
        world.ApplyCommands();

        world.GetComponent<Position>(entity).X.Should().Be(5f);
    }

    [Fact]
    public void Build_TracksNothingByDefault_SameAsPlainWorld()
    {
        var world = new WorldBuilder().Build();
        Entity entity = world.Commands.CreateEntity(new Position());
        world.ApplyCommands();
        world.AdvanceTick();

        world.GetComponent<Position>(entity).X += 1f;

        var (archetype, row) = TestReflection.GetLocation(world, entity);
        var storage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Position>.Value];
        storage.RawLastMarkedTick[row].Should().NotBe(world.CurrentTick);
    }

    [Fact]
    public void WithArchetypeCapacity_SizesEveryArchetypesEntityArray()
    {
        var world = new WorldBuilder().WithArchetypeCapacity(16).Build();
        Entity entity = world.Commands.CreateEntity(new Position());
        world.ApplyCommands();

        var (archetype, _) = TestReflection.GetLocation(world, entity);

        archetype.Entities.Length.Should().Be(16);
    }

    [Fact]
    public void WithArchetypeCapacity_NonPositive_Throws()
    {
        var builder = new WorldBuilder();

        var act = () => builder.WithArchetypeCapacity(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void OnBuilt_IsInvokedOnceWithTheConstructedWorld_AfterBuildReturns()
    {
        var builder = new WorldBuilder();
        World? received = null;
        builder.OnBuilt += w => received = w;

        var world = builder.Build();

        received.Should().BeSameAs(world);
    }

    [Fact]
    public void OnBuilt_WithMultipleSubscribers_InvokesAllOfThemInSubscriptionOrder()
    {
        var builder = new WorldBuilder();
        var order = new List<int>();
        builder.OnBuilt += _ => order.Add(1);
        builder.OnBuilt += _ => order.Add(2);

        builder.Build();

        order.Should().Equal(1, 2);
    }

    [Fact]
    public void OnBuilt_WithNoSubscribers_DoesNotThrow()
    {
        var act = () => new WorldBuilder().Build();

        act.Should().NotThrow();
    }

    [Fact]
    public void Build_CalledTwice_ThrowsInsteadOfCorruptingTheFirstWorld()
    {
        var builder = new WorldBuilder();
        builder.Build();

        var act = () => builder.Build();

        act.Should().Throw<InvalidOperationException>().WithMessage("*already built*");
    }

    [Fact]
    public void WithArchetypeCapacity_AfterBuild_Throws()
    {
        var builder = new WorldBuilder();
        builder.Build();

        var act = () => builder.WithArchetypeCapacity(32);

        act.Should().Throw<InvalidOperationException>().WithMessage("*already built*");
    }

    [Fact]
    public void WithParallelThreshold_AfterBuild_Throws()
    {
        var builder = new WorldBuilder();
        builder.Build();

        var act = () => builder.WithParallelThreshold(0);

        act.Should().Throw<InvalidOperationException>().WithMessage("*already built*");
    }

    [Fact]
    public void WithScheduler_AfterBuild_Throws()
    {
        var builder = new WorldBuilder();
        builder.Build();

        var act = () => builder.WithScheduler(new ParallelSystemScheduler(1000));

        act.Should().Throw<InvalidOperationException>().WithMessage("*already built*");
    }

    [Fact]
    public void AddSystemCore_AfterBuild_Throws()
    {
        var builder = new WorldBuilder();
        builder.Build();

        var act = () => builder.AddSystemCore(typeof(RecordingSystem), null, _ => new RecordingSystem(), [], []);

        act.Should().Throw<InvalidOperationException>().WithMessage("*already built*");
    }

    [Fact]
    public void AddSystemCore_WithFixedCadence_SetsEntryCadenceToFixed()
    {
        var builder = new WorldBuilder();
        var registration = builder.AddSystemCore(typeof(CadenceProbeSystem), access: null, _ => new CadenceProbeSystem(), [], [], cadence: SystemCadence.Fixed);

        registration.Entry.Cadence.Should().Be(SystemCadence.Fixed);
    }

    [Fact]
    public void AddSystemCore_WithoutCadenceArgument_DefaultsToVariable()
    {
        var builder = new WorldBuilder();
        var registration = builder.AddSystemCore(typeof(CadenceProbeSystem), access: null, _ => new CadenceProbeSystem(), [], []);

        registration.Entry.Cadence.Should().Be(SystemCadence.Variable);
    }

    [Fact]
    public void Build_ConstructsDependenciesBeforeDependents_RegardlessOfRegistrationOrder()
    {
        // DependencyLeafSystem registered BEFORE DependencyRootSystem - the adversarial
        // order. Its Construct closure calls world.GetSystem<DependencyRootSystem>() at
        // construction time, so this only passes if Build() actually reorders construction
        // by the declared dependency, not registration order.
        var builder = new WorldBuilder();
        builder.AddSystemCore(
            typeof(DependencyLeafSystem), access: null, w => new DependencyLeafSystem(w),
            generatedBeforeTargets: [], generatedAfterTargets: [],
            constructionDependencies: [typeof(DependencyRootSystem)]);
        builder.AddSystemCore(
            typeof(DependencyRootSystem), access: null, _ => new DependencyRootSystem(),
            generatedBeforeTargets: [], generatedAfterTargets: []);

        var world = builder.Build();

        world.GetSystem<DependencyLeafSystem>().Root.Should().BeSameAs(world.GetSystem<DependencyRootSystem>());
    }

    [Fact]
    public void Build_WithAnUnregisteredConstructionDependency_ThrowsNamingBothTypes()
    {
        var builder = new WorldBuilder();
        builder.AddSystemCore(
            typeof(DependencyLeafSystem), access: null, w => new DependencyLeafSystem(w),
            generatedBeforeTargets: [], generatedAfterTargets: [],
            constructionDependencies: [typeof(DependencyRootSystem)]);
        // DependencyRootSystem is never registered.

        var act = () => builder.Build();

        act.Should().Throw<InvalidOperationException>().WithMessage("*DependencyLeafSystem*DependencyRootSystem*");
    }

    [Fact]
    public void Build_WithACyclicConstructionDependency_ThrowsNamingTheCycle()
    {
        var builder = new WorldBuilder();
        builder.AddSystemCore(
            typeof(CycleASystem), access: null, _ => new CycleASystem(),
            generatedBeforeTargets: [], generatedAfterTargets: [],
            constructionDependencies: [typeof(CycleBSystem)]);
        builder.AddSystemCore(
            typeof(CycleBSystem), access: null, _ => new CycleBSystem(),
            generatedBeforeTargets: [], generatedAfterTargets: [],
            constructionDependencies: [typeof(CycleASystem)]);

        var act = () => builder.Build();

        act.Should().Throw<InvalidOperationException>().WithMessage("*CycleASystem*CycleBSystem*");
    }
}
