namespace Wyrd.Ecs.Tests.Internal;

using Wyrd.Ecs.Internal;

file struct Position : IComponent;
file struct Velocity : IComponent;
file struct Health : IComponent;

file sealed class WriterA : EcsSystem { protected override void Execute(World world, Time time) { } }
file sealed class WriterB : EcsSystem { protected override void Execute(World world, Time time) { } }
file sealed class ReaderC : EcsSystem { protected override void Execute(World world, Time time) { } }
file sealed class UnknownSystem : EcsSystem { protected override void Execute(World world, Time time) { } }

public class StagePlannerTests
{
    private static readonly IReadOnlyDictionary<Type, (IReadOnlyList<Type>, IReadOnlyList<Type>)> NoEdges =
        new Dictionary<Type, (IReadOnlyList<Type>, IReadOnlyList<Type>)>();


    [Fact]
    public void TwoSystemsWritingTheSameComponent_LandInDifferentStages()
    {
        var access = new Dictionary<Type, SystemAccess>
        {
            [typeof(WriterA)] = new(Reads: [], Writes: [typeof(Position)]),
            [typeof(WriterB)] = new(Reads: [], Writes: [typeof(Position)]),
        };
        var systems = new OrderedSystem[] { new WriterA(), new WriterB() };

        var stages = StagePlanner.BuildStages(systems, access, NoEdges);

        stages.Should().HaveCount(2);
        stages[0].Should().ContainSingle();
        stages[1].Should().ContainSingle();
    }

    [Fact]
    public void DisjointComponentSets_LandInTheSameStage()
    {
        var access = new Dictionary<Type, SystemAccess>
        {
            [typeof(WriterA)] = new(Reads: [], Writes: [typeof(Position)]),
            [typeof(WriterB)] = new(Reads: [], Writes: [typeof(Health)]),
        };
        var systems = new OrderedSystem[] { new WriterA(), new WriterB() };

        var stages = StagePlanner.BuildStages(systems, access, NoEdges);

        stages.Should().ContainSingle();
        stages[0].Should().HaveCount(2);
    }

    [Fact]
    public void WriteAndReadOfTheSameComponent_Conflict()
    {
        var access = new Dictionary<Type, SystemAccess>
        {
            [typeof(WriterA)] = new(Reads: [], Writes: [typeof(Velocity)]),
            [typeof(ReaderC)] = new(Reads: [typeof(Velocity)], Writes: []),
        };
        var systems = new OrderedSystem[] { new WriterA(), new ReaderC() };

        var stages = StagePlanner.BuildStages(systems, access, NoEdges);

        stages.Should().HaveCount(2);
    }

    [Fact]
    public void UnknownSystem_AlwaysGetsItsOwnExclusiveStage()
    {
        var access = new Dictionary<Type, SystemAccess>
        {
            [typeof(WriterA)] = new(Reads: [], Writes: [typeof(Health)]),
        };
        var systems = new OrderedSystem[] { new WriterA(), new UnknownSystem() };

        var stages = StagePlanner.BuildStages(systems, access, NoEdges);

        stages.Should().HaveCount(2);
        stages.Should().Contain(stage => stage.Count == 1 && stage[0] is UnknownSystem);
    }

    [Fact]
    public void DynamicDescriptor_ParticipatesInTheSameGraph()
    {
        var access = new Dictionary<Type, SystemAccess>
        {
            [typeof(WriterA)] = new(Reads: [], Writes: [typeof(Health)]),
        };
        var dynamicSystem = new DynamicHealthWriter();
        var systems = new OrderedSystem[] { new WriterA(), dynamicSystem };

        var stages = StagePlanner.BuildStages(systems, access, NoEdges);

        stages.Should().HaveCount(2, "dynamicSystem also writes Health -> conflicts with WriterA");
    }

    private sealed class DynamicHealthWriter : EcsSystem, IQueryAccessDescriptor
    {
        protected override void Execute(World world, Time time) { }
        public SystemAccess DescribeAccess() => new(Reads: [], Writes: [typeof(Health)]);
    }
}
