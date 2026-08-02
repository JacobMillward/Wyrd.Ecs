namespace Wyrd.Ecs;

/// <summary>
/// Marker implemented by <see cref="Query{TShape}"/>. A <c>QuerySystem</c>'s
/// <c>DefineQuery</c> override declares this non-generic type as its return type instead
/// of restating the query chain's exact tuple shape; the generator recovers the shape
/// from the return *expression*, not the declared return type, so this interface carries
/// no members and is never inspected at runtime.
/// </summary>
public interface IQuery;
