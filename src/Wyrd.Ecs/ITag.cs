namespace Wyrd.Ecs;

/// <summary>
/// Marker interface for tag structs — zero-size markers used to include or exclude
/// entities from queries without carrying data. Implement on an empty <c>struct</c>.
/// </summary>
public interface ITag
{
}
