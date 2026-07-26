using System.Reflection;

namespace Wyrd.Ecs.Tests;

/// <summary>
/// Shared reflection helper for tests that peek at a <see cref="World"/>'s internal
/// entity location table — it lives inside <see cref="World"/>'s private
/// <c>Internal.EntityTable</c>, not on <see cref="World"/> itself.
/// </summary>
internal static class TestReflection
{
    internal static (Wyrd.Ecs.Internal.Archetype Archetype, int Row) GetLocation(World world, Entity entity)
    {
        var entityTableField = typeof(World).GetField("_entityTable", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var entityTable = entityTableField.GetValue(world)!;
        var locationsField = entityTable.GetType().GetField("_locations", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var locations = (Wyrd.Ecs.Internal.EntityLocation[])locationsField.GetValue(entityTable)!;
        var location = locations[entity.Id];
        return (location.Archetype, location.Row);
    }

    /// <summary>Peeks at <see cref="World.TotalEntityCount"/>, an <c>internal</c> property.</summary>
    internal static int GetTotalEntityCount(World world)
    {
        var property = typeof(World).GetProperty("TotalEntityCount", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (int)property.GetValue(world)!;
    }
}
