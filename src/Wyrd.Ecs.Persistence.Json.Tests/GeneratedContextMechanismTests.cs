using System.Text.Json;

namespace Wyrd.Ecs.Persistence.Json.Tests;

public struct MechanismPosition : IComponent
{
    public float X;
    public float Y;
}

public class GeneratedContextMechanismTests
{
    [Fact]
    public void TheGeneratedContextClass_RoundTripsARealComponentType()
    {
        var value = new MechanismPosition { X = 3f, Y = 4f };

        var json = JsonSerializer.SerializeToUtf8Bytes(value, Wyrd_Ecs_Persistence_Json_TestsJsonPersistenceContext.Default.Wyrd_Ecs_Persistence_Json_Tests_MechanismPosition);
        var back = JsonSerializer.Deserialize(json, Wyrd_Ecs_Persistence_Json_TestsJsonPersistenceContext.Default.Wyrd_Ecs_Persistence_Json_Tests_MechanismPosition);

        back.X.Should().Be(3f);
        back.Y.Should().Be(4f);
    }
}
