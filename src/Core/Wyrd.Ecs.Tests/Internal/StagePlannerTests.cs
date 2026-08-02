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
    private static SystemEntry EntryFor(EcsSystem instance, SystemAccess? access) =>
        new() { SystemType = instance.GetType(), Construct = _ => instance, Access = access, Instance = instance };

    [Fact]
    public void TwoSystemsWritingTheSameComponent_LandInDifferentStages()
    {
        var writerA = new WriterA();
        var writerB = new WriterB();
        SystemEntry[] entries =
        [
            EntryFor(writerA, new(Reads: [], Writes: [typeof(Position)])),
            EntryFor(writerB, new(Reads: [], Writes: [typeof(Position)])),
        ];

        var stages = StagePlanner.BuildStages(entries);

        stages.Should().HaveCount(2);
        stages[0].Should().ContainSingle();
        stages[1].Should().ContainSingle();
    }

    [Fact]
    public void DisjointComponentSets_LandInTheSameStage()
    {
        var writerA = new WriterA();
        var writerB = new WriterB();
        SystemEntry[] entries =
        [
            EntryFor(writerA, new(Reads: [], Writes: [typeof(Position)])),
            EntryFor(writerB, new(Reads: [], Writes: [typeof(Health)])),
        ];

        var stages = StagePlanner.BuildStages(entries);

        stages.Should().ContainSingle();
        stages[0].Should().HaveCount(2);
    }

    [Fact]
    public void WriteAndReadOfTheSameComponent_Conflict()
    {
        var writerA = new WriterA();
        var readerC = new ReaderC();
        SystemEntry[] entries =
        [
            EntryFor(writerA, new(Reads: [], Writes: [typeof(Velocity)])),
            EntryFor(readerC, new(Reads: [typeof(Velocity)], Writes: [])),
        ];

        var stages = StagePlanner.BuildStages(entries);

        stages.Should().HaveCount(2);
    }

    [Fact]
    public void UnknownSystem_AlwaysGetsItsOwnExclusiveStage()
    {
        var writerA = new WriterA();
        var unknown = new UnknownSystem();
        SystemEntry[] entries =
        [
            EntryFor(writerA, new(Reads: [], Writes: [typeof(Health)])),
            EntryFor(unknown, access: null), // no generated entry at all -> conservative exclusive-stage fallback
        ];

        var stages = StagePlanner.BuildStages(entries);

        stages.Should().HaveCount(2);
        stages.Should().Contain(stage => stage.Count == 1 && stage[0] is UnknownSystem);
    }

    [Fact]
    public void DynamicDescriptor_ParticipatesInTheSameGraph()
    {
        var writerA = new WriterA();
        var dynamicSystem = new DynamicHealthWriter();
        SystemEntry[] entries =
        [
            EntryFor(writerA, new(Reads: [], Writes: [typeof(Health)])),
            EntryFor(dynamicSystem, access: null), // no generated entry -> falls through to IQueryAccessDescriptor
        ];

        var stages = StagePlanner.BuildStages(entries);

        stages.Should().HaveCount(2, "dynamicSystem also writes Health -> conflicts with WriterA");
    }

    private sealed class DynamicHealthWriter : EcsSystem, IQueryAccessDescriptor
    {
        protected override void Execute(World world, Time time) { }
        public SystemAccess DescribeAccess() => new(Reads: [], Writes: [typeof(Health)]);
    }
}
