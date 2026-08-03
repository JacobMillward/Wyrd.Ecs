namespace Wyrd.Ecs.Tests;

public class SystemEntryCadenceTests
{
    [Fact]
    public void NewSystemEntry_DefaultsToVariableCadence()
    {
        var entry = new SystemEntry { SystemType = typeof(object), Construct = _ => null! };

        entry.Cadence.Should().Be(SystemCadence.Variable);
    }

    [Fact]
    public void FixedTimestepAttribute_IsClassScopedNotInheritedNotStackable()
    {
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(typeof(FixedTimestepAttribute), typeof(AttributeUsageAttribute))!;

        usage.ValidOn.Should().Be(AttributeTargets.Class);
        usage.Inherited.Should().BeFalse();
        usage.AllowMultiple.Should().BeFalse();
    }
}
