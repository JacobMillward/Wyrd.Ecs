using System.Text;

namespace Wyrd.Ecs.SystemGenerators;

internal static class QueryChainEmitter
{
    /// <summary>
    /// Emits the shared backend for one *logical* shape (one call per distinct
    /// <see cref="QueryShapeExtensions.DedupKey"/> among the shapes passed to
    /// <see cref="RenderForEachOverload"/>): the cached <c>ArchetypeQuery</c>, the
    /// bespoke delegate type, and the actual per-chunk iteration logic. Reused by
    /// every exact-declaration-order overload sharing this shape.
    /// </summary>
    internal static string RenderBackend(QueryShape shape)
    {
        var hash = shape.HashName();
        var sb = new StringBuilder();
        sb.AppendLine("using Wyrd.Ecs;");
        sb.AppendLine();
        sb.AppendLine("namespace Wyrd.Ecs;");
        sb.AppendLine();

        sb.AppendLine($"internal static class QueryChainBackend_{hash}");
        sb.AppendLine("{");
        sb.AppendLine("    internal static readonly ArchetypeQuery Cached = Build();");
        sb.AppendLine();
        sb.AppendLine("    private static ArchetypeQuery Build()");
        sb.AppendLine("    {");
        sb.AppendLine("        var query = ArchetypeQuery.Empty;");
        foreach (var m in shape.Markers)
        {
            sb.AppendLine(m.Kind == MarkerKind.Has
                ? $"        query = query.Has<{m.ComponentTypeName}>();"
                : $"        query = query.Access<{AccessorType(m)}>();");
        }
        foreach (var w in shape.Withouts)
            sb.AppendLine($"        query = query.Without<{w.TypeName}>();");
        foreach (var a in shape.Anys)
            sb.AppendLine($"        query = query.Any<{a.Type0Name}, {a.Type1Name}>();");
        sb.AppendLine("        return query;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Emits the per-exact-shape extension method overload (one call per distinct
    /// <see cref="QueryShape.ExactShapeTypeName"/>). Its public delegate/parameter list uses
    /// this shape's own declaration order (<see cref="QueryShapeExtensions.OwnDataElements"/>),
    /// not the shared backend's alphabetical order — the body adapts between the two orders
    /// when calling into <see cref="RenderBackend"/>'s backend for this shape's
    /// <see cref="QueryShapeExtensions.DedupKey"/>, so a caller never needs to know or match
    /// the backend's internal ordering.
    /// </summary>
    internal static string RenderForEachOverload(QueryShape shape)
    {
        var hash = shape.HashName();
        var overloadHash = ExactShapeHash(shape);
        var ownElements = shape.OwnDataElements();

        var sb = new StringBuilder();
        sb.AppendLine("using Wyrd.Ecs;");
        sb.AppendLine();
        sb.AppendLine("namespace Wyrd.Ecs;");
        sb.AppendLine();

        var ownActionParams = string.Join(", ", new[] { "TUniform uniform" }.Concat(ownElements.Select(ParamDecl)));
        sb.AppendLine($"internal delegate void QueryChainActionOwn_{overloadHash}<TUniform>({ownActionParams});");
        sb.AppendLine();

        sb.AppendLine($"internal static class QueryChainTerminals_{overloadHash}");
        sb.AppendLine("{");
        sb.AppendLine($"    internal static void ForEach<TUniform>(this {shape.ExactShapeTypeName} query, TUniform uniform, QueryChainActionOwn_{overloadHash}<TUniform> action)");
        sb.AppendLine("    {");
        sb.AppendLine($"        foreach (var chunk in QueryChainBackend_{hash}.Cached.Resolve(query.World))");
        var accessArgs = ownElements.Select(e => $"chunk.Access<{AccessorType(e)}>()");
        var processCallArgs = string.Join(", ", new[] { "uniform", "action", "chunk.Count" }.Concat(accessArgs));
        sb.AppendLine($"            Process({processCallArgs});");
        sb.AppendLine();
        var processParams = string.Join(", ", new[] { "TUniform uniform", $"QueryChainActionOwn_{overloadHash}<TUniform> action", "int count" }.Concat(ownElements.Select(e => $"{AccessorType(e)} {ParamName(e)}")));
        sb.AppendLine($"        static void Process({processParams})");
        sb.AppendLine("        {");
        sb.AppendLine("            for (var i = 0; i < count; i++)");
        var actionCallArgs = string.Join(", ", new[] { "uniform" }.Concat(ownElements.Select(e => $"{RefKind(e)} {ParamName(e)}[i]")));
        sb.AppendLine($"                action({actionCallArgs});");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>The predicate-delegate `.ForEach` overload — same own-order/adapter rules as <see cref="RenderForEachOverload"/>, see Task 8.</summary>
    internal static string RenderPredicateForEachOverload(QueryShape shape)
    {
        var hash = shape.HashName();
        var overloadHash = ExactShapeHash(shape);
        var ownElements = shape.OwnDataElements();

        var sb = new StringBuilder();
        sb.AppendLine("using Wyrd.Ecs;");
        sb.AppendLine();
        sb.AppendLine("namespace Wyrd.Ecs;");
        sb.AppendLine();

        var ownActionParams = string.Join(", ", new[] { "TUniform uniform" }.Concat(ownElements.Select(ParamDecl)));
        sb.AppendLine($"internal delegate bool QueryChainPredicateOwn_{overloadHash}<TUniform>({ownActionParams});");
        sb.AppendLine();

        sb.AppendLine($"internal static class QueryChainPredicateTerminals_{overloadHash}");
        sb.AppendLine("{");
        sb.AppendLine($"    internal static void ForEach<TUniform>(this {shape.ExactShapeTypeName} query, TUniform uniform, QueryChainPredicateOwn_{overloadHash}<TUniform> action)");
        sb.AppendLine("    {");
        sb.AppendLine($"        foreach (var chunk in QueryChainBackend_{hash}.Cached.Resolve(query.World))");
        var accessArgs = ownElements.Select(e => $"chunk.Access<{AccessorType(e)}>()");
        var predicateProcessCallArgs = string.Join(", ", new[] { "uniform", "action", "chunk.Count" }.Concat(accessArgs));
        sb.AppendLine($"            if (!Process({predicateProcessCallArgs})) return;");
        sb.AppendLine();
        var predicateProcessParams = string.Join(", ", new[] { "TUniform uniform", $"QueryChainPredicateOwn_{overloadHash}<TUniform> action", "int count" }.Concat(ownElements.Select(e => $"{AccessorType(e)} {ParamName(e)}")));
        sb.AppendLine($"        static bool Process({predicateProcessParams})");
        sb.AppendLine("        {");
        sb.AppendLine("            for (var i = 0; i < count; i++)");
        var predicateActionCallArgs = string.Join(", ", new[] { "uniform" }.Concat(ownElements.Select(e => $"{RefKind(e)} {ParamName(e)}[i]")));
        sb.AppendLine($"                if (!action({predicateActionCallArgs})) return false;");
        sb.AppendLine("            return true;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>The `.ParallelForEach` overload — same own-order/adapter rules as <see cref="RenderForEachOverload"/>, see Task 9.</summary>
    internal static string RenderParallelForEachOverload(QueryShape shape)
    {
        var hash = shape.HashName();
        var overloadHash = ExactShapeHash(shape);
        var ownElements = shape.OwnDataElements();

        var sb = new StringBuilder();
        sb.AppendLine("using Wyrd.Ecs;");
        sb.AppendLine();
        sb.AppendLine("namespace Wyrd.Ecs;");
        sb.AppendLine();

        sb.AppendLine($"internal static class QueryChainParallelTerminals_{overloadHash}");
        sb.AppendLine("{");
        sb.AppendLine($"    internal static void ParallelForEach<TUniform>(this {shape.ExactShapeTypeName} query, TUniform uniform, QueryChainActionOwn_{overloadHash}<TUniform> action)");
        sb.AppendLine("    {");
        sb.AppendLine("        var chunks = new System.Collections.Generic.List<ArchetypeChunk>();");
        sb.AppendLine($"        foreach (var chunk in QueryChainBackend_{hash}.Cached.Resolve(query.World)) chunks.Add(chunk);");
        sb.AppendLine();
        sb.AppendLine("        System.Threading.Tasks.Parallel.ForEach(chunks, chunk =>");
        var parallelProcessCallArgs = string.Join(", ", new[] { "uniform", "action", "chunk.Count" }.Concat(ownElements.Select(e => $"chunk.Access<{AccessorType(e)}>()")));
        sb.AppendLine($"            Process({parallelProcessCallArgs}));");
        sb.AppendLine();
        var parallelProcessParams = string.Join(", ", new[] { "TUniform uniform", $"QueryChainActionOwn_{overloadHash}<TUniform> action", "int count" }.Concat(ownElements.Select(e => $"{AccessorType(e)} {ParamName(e)}")));
        sb.AppendLine($"        static void Process({parallelProcessParams})");
        sb.AppendLine("        {");
        sb.AppendLine("            for (var i = 0; i < count; i++)");
        var parallelActionCallArgs = string.Join(", ", new[] { "uniform" }.Concat(ownElements.Select(e => $"{RefKind(e)} {ParamName(e)}[i]")));
        sb.AppendLine($"                action({parallelActionCallArgs});");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>Emits the <c>GeneratedSystemAccess</c> registry the static-parallel-scheduler plan's scheduler consumes.</summary>
    internal static string RenderSystemAccessRegistry(IReadOnlyList<(string SystemTypeName, List<string> Reads, List<string> Writes)> systems)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using Wyrd.Ecs;");
        sb.AppendLine();
        sb.AppendLine("namespace Wyrd.Ecs.Generated;");
        sb.AppendLine();
        sb.AppendLine("public static class GeneratedSystemAccess");
        sb.AppendLine("{");
        sb.AppendLine("    public static readonly IReadOnlyDictionary<Type, SystemAccess> Entries = new Dictionary<Type, SystemAccess>");
        sb.AppendLine("    {");
        foreach (var system in systems)
        {
            var reads = string.Join(", ", system.Reads.Select(t => $"typeof({t})"));
            var writes = string.Join(", ", system.Writes.Select(t => $"typeof({t})"));
            sb.AppendLine($"        [typeof(global::{system.SystemTypeName})] = new(Reads: new Type[] {{ {reads} }}, Writes: new Type[] {{ {writes} }}),");
        }
        sb.AppendLine("    };");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Emits the `partial` class part supplying a `QuerySystem` subclass's `Execute`
    /// declaration (as a required partial method — an explicit access modifier makes
    /// C# 9+ treat a partial method as needing an implementation, confirmed via
    /// `partial-method-check/`) and its `EcsSystem.OnUpdate` implementation.
    /// </summary>
    internal static string RenderQuerySystemGlue(QuerySystemCandidate candidate)
    {
        var dataElements = candidate.Shape.DataElements();
        var executeParams = string.Join(", ", new[] { "ulong tick" }.Concat(dataElements.Select(ParamDecl)));
        // Calling a ref/in parameter requires the same modifier at the call site, not
        // just on the parameter declaration -- RefKind(e) here, not a bare ParamName(e).
        // Both lists are built by prepending "ulong t"/"ulong tick" into the *same*
        // list before joining (matching RenderBackend's actionParams pattern), not by
        // joining dataElements alone and string-concatenating a separator afterward --
        // the latter produces a trailing comma ("(ulong t, )") when dataElements is
        // empty (a filter-only shape with no Writes/Reads at all), which doesn't compile.
        var lambdaParams = string.Join(", ", new[] { "ulong t" }.Concat(dataElements.Select(ParamDecl)));
        var executeCallArgs = string.Join(", ", new[] { "t" }.Concat(dataElements.Select(e => $"{RefKind(e)} {ParamName(e)}")));

        var sb = new StringBuilder();
        sb.AppendLine("using Wyrd.Ecs;");
        sb.AppendLine();

        var hasNamespace = candidate.Namespace.Length > 0;
        if (hasNamespace)
        {
            sb.AppendLine($"namespace {candidate.Namespace};");
            sb.AppendLine();
        }

        sb.AppendLine($"partial class {candidate.ClassName}");
        sb.AppendLine("{");
        sb.AppendLine($"    private partial void Execute({executeParams});");
        sb.AppendLine();
        sb.AppendLine("    protected override void OnUpdate(World world, ulong tick) =>");
        sb.AppendLine($"        Build(world).ForEach(tick, ({lambdaParams}) => Execute({executeCallArgs}));");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>A stable, valid-C#-identifier suffix derived from <see cref="QueryShape.ExactShapeTypeName"/> — distinct from <see cref="QueryShapeExtensions.HashName"/>, which is derived from the order-independent <see cref="QueryShapeExtensions.DedupKey"/> instead.</summary>
    internal static string ExactShapeHash(QueryShape shape)
    {
        var hash = 2166136261u;
        foreach (var c in shape.ExactShapeTypeName)
        {
            hash ^= c;
            hash *= 16777619u;
        }
        return hash.ToString("x8");
    }

    private static string AccessorType(MarkerElement e) => $"{(e.Kind == MarkerKind.Writes ? "Mut" : "Ref")}<{e.ComponentTypeName}>";
    private static string RefKind(MarkerElement e) => e.Kind == MarkerKind.Writes ? "ref" : "in";
    private static string ParamDecl(MarkerElement e) => $"{RefKind(e)} {e.ComponentTypeName} {ParamName(e)}";

    private static string ParamName(MarkerElement e)
    {
        var name = e.ComponentTypeName;
        var simple = name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name;
        return char.ToLowerInvariant(simple[0]) + simple[1..];
    }
}
