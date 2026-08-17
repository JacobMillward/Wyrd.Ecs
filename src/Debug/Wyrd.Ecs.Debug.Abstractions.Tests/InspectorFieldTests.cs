using System.Text.Json;
using Wyrd.Ecs.Debug.Abstractions;

namespace Wyrd.Ecs.Debug.Abstractions.Tests;

public class InspectorFieldTests
{
    [Fact]
    public void ASlider_SerializesWithASliderKindDiscriminator()
    {
        InspectorField field = new InspectorField.Slider("Current", 3, 0, 10);

        var json = JsonSerializer.Serialize(field);

        json.Should().Contain("\"kind\":\"slider\"");
        json.Should().Contain("\"Value\":3");
    }

    [Fact]
    public void AGroup_NestsItsChildrenWithTheirOwnKindDiscriminators()
    {
        InspectorField field = new InspectorField.Group("Health", [
            new InspectorField.Slider("Current", 3, 0, 10),
            new InspectorField.ReadOnly("Max", "10"),
        ]);

        var json = JsonSerializer.Serialize(field);

        json.Should().Contain("\"kind\":\"group\"");
        json.Should().Contain("\"kind\":\"slider\"");
        json.Should().Contain("\"kind\":\"readOnly\"");
    }

    [Fact]
    public void InspectorEdit_AsInt_CoercesAJsonNumber()
    {
        var element = JsonDocument.Parse("7").RootElement;
        var edit = new InspectorEdit("Current", element);

        edit.AsInt().Should().Be(7);
    }
}
