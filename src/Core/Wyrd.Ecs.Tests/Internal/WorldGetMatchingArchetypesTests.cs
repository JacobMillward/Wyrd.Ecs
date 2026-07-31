using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs.Tests.Internal;

file struct Marker : ITag;

public class WorldGetMatchingArchetypesTests
{
    [Fact]
    public void DifferentFilters_WithSameRequiredSignature_DoNotShareCacheEntry()
    {
        var world = new World();
        var tagged = world.Commands.CreateEntity().Entity;
        world.Commands.AddTag<Marker>(tagged);
        var untagged = world.Commands.CreateEntity().Entity;
        world.ApplyCommands();

        var required = ArchetypeSignature.Empty;
        var withoutMarker = ArchetypeFilter.Empty.Without<Marker>();

        var everyoneMatches = world.GetMatchingArchetypes(required, ArchetypeFilter.Empty);
        var filteredMatches = world.GetMatchingArchetypes(required, withoutMarker);

        everyoneMatches.Sum(a => a.Count).Should().Be(2);
        filteredMatches.Sum(a => a.Count).Should().Be(1);
    }
}
