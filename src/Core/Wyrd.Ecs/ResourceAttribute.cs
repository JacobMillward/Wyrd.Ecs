namespace Wyrd.Ecs;

/// <summary>
/// Marks a <see cref="QuerySystem"/> property as auto-refreshed from the current
/// <see cref="World"/> resource of that type once per tick. A get-only or private-set
/// property is read-only (fetched before <c>Update</c> runs); a public setter additionally
/// writes the property's value back to <see cref="World"/> after <c>Update</c> returns.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ResourceAttribute : Attribute
{
}
