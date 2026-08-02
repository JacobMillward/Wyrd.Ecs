namespace Wyrd.Ecs.Tests;

using Wyrd.Ecs.Internal;

public class EventChannelTests
{
    private readonly record struct Ping(int Value);

    [Fact]
    public void Read_SeesAWriteFromTheSameGeneration()
    {
        var channel = new EventChannel<Ping>();
        var cursor = channel.SnapshotCursor();
        channel.Write(new Ping(1));

        var destination = new List<Ping>();
        channel.Read(cursor, destination);

        destination.Should().BeEquivalentTo([new Ping(1)]);
    }

    [Fact]
    public void Read_StillSeesAWriteAfterExactlyOneSwap()
    {
        var channel = new EventChannel<Ping>();
        var cursor = channel.SnapshotCursor();
        channel.Write(new Ping(1));
        channel.Swap();

        var destination = new List<Ping>();
        channel.Read(cursor, destination);

        destination.Should().BeEquivalentTo([new Ping(1)], "one Swap() moves the write into _older, still within the retained window");
    }

    [Fact]
    public void Read_NoLongerSeesAWriteAfterTwoSwaps()
    {
        var channel = new EventChannel<Ping>();
        var cursor = channel.SnapshotCursor();
        channel.Write(new Ping(1));
        channel.Swap();
        channel.Swap();

        var destination = new List<Ping>();
        channel.Read(cursor, destination);

        destination.Should().BeEmpty("a second Swap() retires _older, dropping anything only ever written into it");
    }

    [Fact]
    public void Read_AdvancesTheCursorSoARepeatedReadDoesNotSeeTheSameEventTwice()
    {
        var channel = new EventChannel<Ping>();
        var cursor = channel.SnapshotCursor();
        channel.Write(new Ping(1));

        var destination = new List<Ping>();
        cursor = channel.Read(cursor, destination);
        channel.Read(cursor, destination);

        destination.Should().BeEmpty("the second Read() call started from the cursor the first call returned, past the only write so far");
    }

    [Fact]
    public void Read_TwoIndependentCursorsOnTheSameChannelDoNotInterfere()
    {
        var channel = new EventChannel<Ping>();
        var cursorA = channel.SnapshotCursor();
        channel.Write(new Ping(1));
        var cursorB = channel.SnapshotCursor();
        channel.Write(new Ping(2));

        var destinationA = new List<Ping>();
        var destinationB = new List<Ping>();
        channel.Read(cursorA, destinationA);
        channel.Read(cursorB, destinationB);

        destinationA.Should().BeEquivalentTo([new Ping(1), new Ping(2)], "cursorA was taken before either write");
        destinationB.Should().BeEquivalentTo([new Ping(2)], "cursorB was taken after the first write, before the second");
    }

    [Fact]
    public void Read_ReusesTheDestinationListsBackingArrayAcrossCalls()
    {
        var channel = new EventChannel<Ping>();
        var destination = new List<Ping>();

        var cursor = channel.SnapshotCursor();
        for (var i = 0; i < 8; i++) channel.Write(new Ping(i));
        cursor = channel.Read(cursor, destination);
        var capacityAfterFirstRead = destination.Capacity;

        for (var i = 0; i < 8; i++) channel.Write(new Ping(i));
        channel.Read(cursor, destination);

        destination.Capacity.Should().Be(capacityAfterFirstRead, "Read() clears and refills the same list rather than allocating a new one each call");
    }
}
