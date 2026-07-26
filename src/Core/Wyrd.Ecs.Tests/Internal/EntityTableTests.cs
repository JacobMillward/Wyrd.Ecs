using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs.Tests.Internal;

public class EntityTableTests
{
    [Fact]
    public void Place_OutOfOrderWithinABatch_DoesNotPrematurelyExposeAnEarlierReservedIdAsAlive()
    {
        var table = new EntityTable();
        var archetype = new Archetype(ArchetypeSignature.Empty, 4);

        var a = table.Reserve();
        var b = table.Reserve();

        table.Place(b, archetype);

        table.IsAlive(a.Id, a.Generation).Should().BeFalse();
        table.IsAlive(b.Id, b.Generation).Should().BeTrue(); // b must be alive immediately from its own Place call, regardless of a's

        table.Place(a, archetype);

        table.IsAlive(a.Id, a.Generation).Should().BeTrue();
        table.IsAlive(b.Id, b.Generation).Should().BeTrue();
    }
}
