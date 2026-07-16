namespace Wyrd.Ecs.Tests;

public class InterceptorTests
{
    internal struct Position : IComponent
    {
        public float X;
    }

    [Fact]
    public void PureRead_IsIntercepted()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 3f;
        world.AdvanceTick();

        var total = 0f;
        foreach (var row in world.Query<Position>())
            total += row.Get<Position>().X;

        total.Should().Be(3f);
        MarkedThisTick(world, entity).Should().BeFalse();
    }

    [Fact]
    public void DirectCompoundAssignment_IsNotIntercepted()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 1f;
        world.AdvanceTick();

        foreach (var row in world.Query<Position>())
            row.Get<Position>().X += 1f;

        MarkedThisTick(world, entity).Should().BeTrue();
    }

    [Fact]
    public void RefLocal_PureReadAfterBinding_IsIntercepted()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 5f;
        world.AdvanceTick();

        var total = 0f;
        foreach (var row in world.Query<Position>())
        {
            ref var position = ref row.Get<Position>();
            total += position.X;
            total += position.X;
        }

        total.Should().Be(10f);
        MarkedThisTick(world, entity).Should().BeFalse();
    }

    [Fact]
    public void RefLocal_MutatedAfterBinding_IsNotIntercepted()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 1f;
        world.AdvanceTick();

        foreach (var row in world.Query<Position>())
        {
            ref var position = ref row.Get<Position>();
            var read = position.X;
            position.X = read + 1f;
        }

        MarkedThisTick(world, entity).Should().BeTrue();
    }

    [Fact]
    public void PassedByIn_IsIntercepted()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 2f;
        world.AdvanceTick();

        var total = 0f;
        foreach (var row in world.Query<Position>())
            total += ReadX(in row.Get<Position>());

        total.Should().Be(2f);
        MarkedThisTick(world, entity).Should().BeFalse();
    }

    [Fact]
    public void PassedByRefToAHelperThatOnlyReads_IsIntercepted()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 4f;
        world.AdvanceTick();

        var total = 0f;
        foreach (var row in world.Query<Position>())
            total += ReadXByRef(ref row.Get<Position>());

        total.Should().Be(4f);
        MarkedThisTick(world, entity).Should().BeFalse();
    }

    [Fact]
    public void PassedByRefToAHelperThatWrites_IsNotIntercepted()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 1f;
        world.AdvanceTick();

        foreach (var row in world.Query<Position>())
            IncrementX(ref row.Get<Position>());

        MarkedThisTick(world, entity).Should().BeTrue();
    }

    [Fact]
    public void PassedToAnInterfaceMethod_IsNotIntercepted()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 1f;
        world.AdvanceTick();

        IPositionReader reader = new PositionReader();
        foreach (var row in world.Query<Position>())
            reader.Read(ref row.Get<Position>());

        MarkedThisTick(world, entity).Should().BeTrue();
    }

    private static float ReadX(in Position position) => position.X;
    private static float ReadXByRef(ref Position position) => position.X;
    private static void IncrementX(ref Position position) => position.X += 1f;

    private interface IPositionReader
    {
        void Read(ref Position position);
    }

    private sealed class PositionReader : IPositionReader
    {
        public void Read(ref Position position) => _ = position.X;
    }

    private static bool MarkedThisTick(World world, Entity entity)
    {
        var field = typeof(World).GetField("_locations", global::System.Reflection.BindingFlags.NonPublic | global::System.Reflection.BindingFlags.Instance)!;
        var locations = ((Wyrd.Ecs.Internal.Archetype Archetype, int Row)[])field.GetValue(world)!;
        var (archetype, row) = locations[entity.Id];
        var storage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Position>.Value];
        return storage.RawLastMarkedTick[row] == world.CurrentTick;
    }
}
