using System.Text.Json;
using Wyrd.Ecs.Debug.Internal;

namespace Wyrd.Ecs.Debug.Tests.Internal;

public class FieldMergeTests
{
    [Fact]
    public void MergeField_ReplacesOnlyTheNamedField()
    {
        var original = JsonSerializer.SerializeToUtf8Bytes(new { Current = 3, Max = 10 });
        var newValue = JsonDocument.Parse("7").RootElement;

        var merged = FieldMerge.MergeField(original, "Current", newValue);

        var document = JsonDocument.Parse(merged);
        document.RootElement.GetProperty("Current").GetInt32().Should().Be(7);
        document.RootElement.GetProperty("Max").GetInt32().Should().Be(10);
    }

    [Fact]
    public void MergeField_WhenTheFieldDoesNotExistYet_AddsIt()
    {
        var original = JsonSerializer.SerializeToUtf8Bytes(new { Current = 3 });
        var newValue = JsonDocument.Parse("\"active\"").RootElement;

        var merged = FieldMerge.MergeField(original, "Status", newValue);

        var document = JsonDocument.Parse(merged);
        document.RootElement.GetProperty("Status").GetString().Should().Be("active");
    }
}
