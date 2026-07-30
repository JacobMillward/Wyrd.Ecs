using Microsoft.CodeAnalysis;

namespace Wyrd.Ecs.Generators.Diagnostics;

internal static class WyrdDiagnostics
{
    internal static readonly DiagnosticDescriptor BareDataParameter = new(
        id: "WYRD001",
        title: "Query data parameter must be ref or in",
        messageFormat: "Parameter '{0}' must be declared 'ref' or 'in' to specify whether this query writes or reads '{1}'",
        category: "Wyrd.Ecs.QueryChain",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor UpdateShapeMismatch = new(
        id: "WYRD002",
        title: "QuerySystem.Update does not match DefineQuery's declared components",
        messageFormat: "'{0}.Update' must take Time followed by exactly {1} ({2}), in that order",
        category: "Wyrd.Ecs.QueryChain",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// Two <c>.ForEach</c>/<c>.ParallelForEach</c> call sites (or a <c>QuerySystem</c> and a
    /// call site) share the exact same <c>Query&lt;TShape&gt;</c> closed type — same
    /// <c>.With&lt;T&gt;()</c> set, same declaration order — but resolve a different
    /// <c>ref</c>/<c>in</c> for the same component. Since <c>.Without</c>/<c>.Has</c>/<c>.Any</c>
    /// no longer affect <c>TShape</c>, this is now reachable by two otherwise-unrelated
    /// queries (e.g. one writing a component, one only reading it) that happen to declare
    /// the exact same <c>.With&lt;T&gt;()</c> set with nothing else distinguishing them.
    /// </summary>
    internal static readonly DiagnosticDescriptor ConflictingAccessForSameShape = new(
        id: "WYRD003",
        title: "Two query terminals disagree on read/write access for the same shape",
        messageFormat: "Multiple '.ForEach'/'.ParallelForEach' call sites (or QuerySystems) share the shape '{0}' but resolve different ref/in access for one or more components. Give at least one of them a different '.With<T>()' set so they no longer share an identical Query<TShape> type.",
        category: "Wyrd.Ecs.QueryChain",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// A `file`-scoped type (C# 11 `file` modifier) was used as a query component. This can
    /// never work: the query-chain generator's `.ForEach`/`.ParallelForEach` extension methods
    /// (and `QuerySystem` glue) are emitted into a *separate* generated source file, which
    /// structurally cannot reference a `file`-scoped type declared in the consumer's own file
    /// — regardless of whether any other type happens to share its name. Reported before shape
    /// extraction succeeds, so a rejected shape never reaches the exact-shape/dedup pipeline at
    /// all (see <c>QueryChainGenerator</c>), which also rules out it silently colliding with an
    /// unrelated, ordinarily-scoped type of the same simple name elsewhere in the compilation.
    /// </summary>
    internal static readonly DiagnosticDescriptor FileLocalComponentType = new(
        id: "WYRD004",
        title: "A type scoped to a file cannot be used as a query component",
        messageFormat: "'{0}' is scoped to this file (the 'file' modifier) and cannot be used as a query component. The generator's terminals are emitted into a separate generated file, which can never reference a type scoped to a different file.",
        category: "Wyrd.Ecs.QueryChain",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
