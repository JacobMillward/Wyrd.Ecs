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
    /// A `file`-scoped type (C# 11 `file` modifier) was used as a query component. This can
    /// never work: the generator's `.ForEach`/`.ParallelForEach` extensions (and
    /// `QuerySystem` glue) are emitted into a separate generated source file, which cannot
    /// reference a `file`-scoped type from the consumer's own file. Reported before shape
    /// extraction succeeds, so a rejected shape never reaches the dedup pipeline.
    /// </summary>
    internal static readonly DiagnosticDescriptor FileLocalComponentType = new(
        id: "WYRD004",
        title: "A type scoped to a file cannot be used as a query component",
        messageFormat: "'{0}' is scoped to this file (the 'file' modifier) and cannot be used as a query component. The generator's terminals are emitted into a separate generated file, which can never reference a type scoped to a different file.",
        category: "Wyrd.Ecs.QueryChain",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// A system type has no constructor shape <c>AddSystem&lt;T&gt;()</c> can use: neither a
    /// public <c>ctor(World)</c> nor a public parameterless constructor, or more than one
    /// public constructor (ambiguous which to prefer). No <c>SystemRegistry.Construct</c>
    /// entry is emitted for it, so <c>AddSystem&lt;T&gt;()</c> for it is a compile error at
    /// the call site, not a runtime one - use <c>AddSystem&lt;T&gt;(Func&lt;World, T&gt;)</c>
    /// instead.
    /// </summary>
    internal static readonly DiagnosticDescriptor UnconstructableSystem = new(
        id: "WYRD005",
        title: "System type has no usable constructor for AddSystem<T>()",
        messageFormat: "'{0}' has no public ctor(World) and no public parameterless constructor (or has more than one public constructor), so AddSystem<{0}>() cannot construct it. Register it with AddSystem<{0}>(Func<World, {0}> configure) instead.",
        category: "Wyrd.Ecs.QueryChain",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor ResourcePropertyWrongType = new(
        id: "WYRD006",
        title: "[Resource] property type must implement IResource",
        messageFormat: "Property '{0}' is marked [Resource] but its type '{1}' does not implement IResource",
        category: "Wyrd.Ecs.QueryChain",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor ResourceOnNonQuerySystem = new(
        id: "WYRD007",
        title: "[Resource] is only supported on QuerySystem",
        messageFormat: "Property '{0}' is marked [Resource], but its containing type '{1}' does not derive from QuerySystem. Use World.GetResource<T>()/GetResourceRef<T>() inside Execute instead.",
        category: "Wyrd.Ecs.QueryChain",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor StaleResourceSnapshot = new(
        id: "WYRD008",
        title: "Resource value stored where it will go stale",
        messageFormat: "{0}",
        category: "Wyrd.Ecs.QueryChain",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor UnusedResourceWriteAccess = new(
        id: "WYRD009",
        title: "[Resource] property declares write access but is never assigned",
        messageFormat: "Property '{0}' has a public setter, declaring write access to the scheduler, but no method on '{1}' ever assigns to it. Remove the public setter (read-only) unless a write is intended.",
        category: "Wyrd.Ecs.QueryChain",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

}
