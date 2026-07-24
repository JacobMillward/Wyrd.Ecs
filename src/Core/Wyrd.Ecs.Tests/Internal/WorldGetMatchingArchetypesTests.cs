using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs.Tests.Internal;

file struct Marker : ITag;

public class WorldGetMatchingArchetypesTests
{
    [Fact]
    public void DifferentFilters_WithSameRequiredSignature_DoNotShareCacheEntry()
    {
        var world = new World();
        var tagged = world.Commands.CreateEntity();
        world.Commands.AddTag<Marker>(tagged);
        var untagged = world.Commands.CreateEntity();
        world.ApplyCommands();

        var required = ArchetypeSignature.Empty;
        var withoutMarker = QueryFilter.Empty.Without<Marker>();

        var everyoneMatches = world.GetMatchingArchetypes(required, QueryFilter.Empty);
        var filteredMatches = world.GetMatchingArchetypes(required, withoutMarker);

        everyoneMatches.Sum(a => a.Count).Should().Be(2);
        filteredMatches.Sum(a => a.Count).Should().Be(1);
    }
}
