namespace Wyrd.Ecs.Persistence.Json.Generators.Tests;

public class ConsumerContextNamingTests
{
    [Fact]
    public void ContextClassName_AppendsSuffixToAValidIdentifierAssemblyName()
    {
        ConsumerContextNaming.ContextClassName("MyGame").Should().Be("MyGameJsonPersistenceContext");
    }

    [Fact]
    public void ContextClassName_ReplacesNonIdentifierCharacters()
    {
        ConsumerContextNaming.ContextClassName("My.Game-Simulation").Should().Be("My_Game_SimulationJsonPersistenceContext");
    }

    [Fact]
    public void ContextClassName_PrefixesAnUnderscoreWhenTheNameWouldStartWithADigit()
    {
        ConsumerContextNaming.ContextClassName("3DGame").Should().Be("_3DGameJsonPersistenceContext");
    }

    [Fact]
    public void TypeInfoPropertyName_ReplacesDotsInAFullyQualifiedTypeName()
    {
        ConsumerContextNaming.TypeInfoPropertyName("MyGame.Components.Position").Should().Be("MyGame_Components_Position");
    }

    [Fact]
    public void TypeInfoPropertyName_ForTwoDifferentlyNamespacedTypesWithTheSameSimpleName_ProducesDistinctNames()
    {
        var first = ConsumerContextNaming.TypeInfoPropertyName("MyGame.Components.Position");
        var second = ConsumerContextNaming.TypeInfoPropertyName("MyGame.Other.Position");

        first.Should().NotBe(second);
    }
}
