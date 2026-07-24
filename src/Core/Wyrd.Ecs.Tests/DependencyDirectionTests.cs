namespace Wyrd.Ecs.Tests;

public class DependencyDirectionTests
{
    [Fact]
    public void EngineAssembly_ReferencesNoGameAssembly()
    {
        var engineAssembly = typeof(AssemblyMarker).Assembly;

        var gameReferences = engineAssembly.GetReferencedAssemblies()
            .Where(reference => reference.Name is not null &&
                                 reference.Name.StartsWith("Game", StringComparison.Ordinal))
            .ToList();

        gameReferences.Should().BeEmpty("the engine must never depend on Game-specific types");
    }
}
