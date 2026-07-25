namespace Wyrd.Ecs.SystemGenerators;

/// <summary>
/// One discovered `QuerySystem` subclass: its containing namespace (empty string for
/// the global namespace), simple class name, and the shape extracted from its `Build`
/// method's return type. Only top-level classes are supported — a `QuerySystem`
/// subclass nested inside another type has no candidate produced for it at all (see
/// <see cref="QueryChainGenerator"/>'s `Initialize`), since re-declaring a matching
/// nested `partial` chain correctly is out of scope for this design.
/// </summary>
internal sealed class QuerySystemCandidate
{
    public required string Namespace { get; init; }
    public required string ClassName { get; init; }
    public required QueryShape Shape { get; init; }
}
