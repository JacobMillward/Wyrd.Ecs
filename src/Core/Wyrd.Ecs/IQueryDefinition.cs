namespace Wyrd.Ecs;

/// <summary>
/// Marker implemented by <see cref="Query{TShape}"/>. A <c>QuerySystem</c>'s <c>Build</c>
/// method declares this non-generic type as its return type instead of restating the
/// query chain's exact tuple shape — the query-chain generator recovers the shape from
/// <c>Build</c>'s return *expression*, not its declared return type (see
/// <c>QueryChainGenerator.TryExtractQuerySystem</c>), so this interface carries no
/// members and is never inspected at runtime. A struct implementing an interface
/// converts to it implicitly, so `Build`'s existing single-expression bodies need no
/// change beyond their declared return type.
/// </summary>
public interface IQueryDefinition;
