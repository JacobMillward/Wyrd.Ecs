namespace Wyrd.Ecs.Tests;

public class EventTests
{
    private readonly record struct Damage(int Amount) : IEvent;

    [Fact]
    public void Emit_AndCreateEventReader_WorkWithNoWorldBuilderSetup()
    {
        var world = new World();
        var reader = world.CreateEventReader<Damage>();

        world.Emit(new Damage(5));

        reader.Read().Should().BeEquivalentTo([new Damage(5)]);
    }

    [Fact]
    public void CreateEventReader_DoesNotSeeAnEmissionFromBeforeItWasCreated()
    {
        var world = new World();
        world.Emit(new Damage(5));

        var reader = world.CreateEventReader<Damage>();

        reader.Read().Should().BeEmpty("the reader was created after the emission");
    }

    [Fact]
    public void TwoIndependentReaders_EachSeeEveryEmissionOnce()
    {
        var world = new World();
        var readerA = world.CreateEventReader<Damage>();
        var readerB = world.CreateEventReader<Damage>();

        world.Emit(new Damage(1));
        readerA.Read();
        world.Emit(new Damage(2));

        readerA.Read().Should().BeEquivalentTo([new Damage(2)], "readerA already drained the first emission");
        readerB.Read().Should().BeEquivalentTo([new Damage(1), new Damage(2)], "readerB has never drained, so it sees both");
    }

    [Fact]
    public void Emit_FromManyThreadsConcurrently_LosesNoWrites()
    {
        var world = new World();
        var reader = world.CreateEventReader<Damage>(); // channel already exists before the race below

        Parallel.For(0, 500, i => world.Emit(new Damage(i)));

        reader.Read().Should().HaveCount(500, "every one of the 500 concurrent Emit calls must survive, none lost to a race");
    }

    [Fact]
    public void GetOrCreateEventChannel_ConcurrentFirstUseOfABrandNewType_ReturnsTheSameChannelToEveryCaller()
    {
        var world = new World();
        var channels = new Wyrd.Ecs.Internal.EventChannel<Damage>[500];

        Parallel.For(0, 500, i => channels[i] = world.GetOrCreateEventChannel<Damage>());

        channels.Should().OnlyContain(c => ReferenceEquals(c, channels[0]), "every concurrent first-use call for the same never-before-used type must resolve to exactly one EventChannel<Damage> instance, not a distinct one per racing thread");
    }

    [Fact]
    public void Read_MissesASameTickLateEmission_ButCatchesItNextTick()
    {
        var world = new World();
        var reader = world.CreateEventReader<Damage>();

        _ = reader.Read(); // this reader's one read this tick - its stage ran before the emitter's
        world.Emit(new Damage(1)); // emitter's stage runs later, same tick - too late for the read above to catch

        world.AdvanceTick(); // moves to the next tick; exactly one Swap() has now happened

        reader.Read().Should().BeEquivalentTo([new Damage(1)], "one AdvanceTick() since the emission is still within the two-swap retention window");
    }

    [Fact]
    public void Read_SkippingAnEntireTickBetweenCallsSilentlyLosesWhatWasEmittedBeforeTheGap()
    {
        var world = new World();
        var reader = world.CreateEventReader<Damage>();

        _ = reader.Read();
        world.Emit(new Damage(1)); // same tick, too late for the read above to catch

        world.AdvanceTick(); // tick N -> N+1 (first Swap())
        // reader does NOT call Read() here - this is the skipped tick
        world.AdvanceTick(); // tick N+1 -> N+2 (second Swap() retires it)

        reader.Read().Should().BeEmpty("the reader skipped a full tick between calls, so the second AdvanceTick()'s Swap() retired the emission before this Read() ever ran - this is NOT safe to rely on as \"poll every other tick\"");
    }
}
