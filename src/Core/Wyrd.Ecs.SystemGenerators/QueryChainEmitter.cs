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
        var dataElements = shape.DataElements();
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using Wyrd.Ecs;");
        sb.AppendLine();
        sb.AppendLine("namespace Wyrd.Ecs;");
        sb.AppendLine();

        var actionParams = string.Join(", ", new[] { "TUniform uniform" }.Concat(dataElements.Select(ParamDecl)));
        sb.AppendLine($"public delegate void QueryChainAction_{hash}<TUniform>({actionParams});");
        sb.AppendLine($"public delegate bool QueryChainPredicate_{hash}<TUniform>({actionParams});");
        sb.AppendLine();

        sb.AppendLine($"internal static class QueryChainWorker_{hash}");
        sb.AppendLine("{");
        sb.AppendLine("    private static readonly ArchetypeQuery Cached = Build();");
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
        sb.AppendLine();

        sb.AppendLine($"    internal static void RunForEach<TUniform>(World world, TUniform uniform, QueryChainAction_{hash}<TUniform> action)");
        sb.AppendLine("    {");
        sb.AppendLine("        foreach (var chunk in Cached.Resolve(world))");
        var accessArgs = dataElements.Select(e => $"chunk.Access<{AccessorType(e)}>()");
        var processCallArgs = string.Join(", ", new[] { "uniform", "action", "chunk.Count" }.Concat(accessArgs));
        sb.AppendLine($"            Process({processCallArgs});");
        sb.AppendLine();
        var processParams = string.Join(", ", new[] { "TUniform uniform", $"QueryChainAction_{hash}<TUniform> action", "int count" }.Concat(dataElements.Select(e => $"{AccessorType(e)} {ParamName(e)}")));
        sb.AppendLine($"        static void Process({processParams})");
        sb.AppendLine("        {");
        sb.AppendLine("            for (var i = 0; i < count; i++)");
        var actionCallArgs = string.Join(", ", new[] { "uniform" }.Concat(dataElements.Select(e => $"{RefKind(e)} {ParamName(e)}[i]")));
        sb.AppendLine($"                action({actionCallArgs});");
        sb.AppendLine("        }");
        sb.AppendLine("    }");

        sb.AppendLine();
        sb.AppendLine($"    internal static void RunForEachPredicate<TUniform>(World world, TUniform uniform, QueryChainPredicate_{hash}<TUniform> action)");
        sb.AppendLine("    {");
        sb.AppendLine("        foreach (var chunk in Cached.Resolve(world))");
        var predicateProcessCallArgs = string.Join(", ", new[] { "uniform", "action", "chunk.Count" }.Concat(accessArgs));
        sb.AppendLine($"            if (!Process({predicateProcessCallArgs})) return;");
        sb.AppendLine();
        var predicateProcessParams = string.Join(", ", new[] { "TUniform uniform", $"QueryChainPredicate_{hash}<TUniform> action", "int count" }.Concat(dataElements.Select(e => $"{AccessorType(e)} {ParamName(e)}")));
        sb.AppendLine($"        static bool Process({predicateProcessParams})");
        sb.AppendLine("        {");
        sb.AppendLine("            for (var i = 0; i < count; i++)");
        var predicateActionCallArgs = string.Join(", ", new[] { "uniform" }.Concat(dataElements.Select(e => $"{RefKind(e)} {ParamName(e)}[i]")));
        sb.AppendLine($"                if (!action({predicateActionCallArgs})) return false;");
        sb.AppendLine("            return true;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");

        sb.AppendLine();
        sb.AppendLine($"    internal static void RunParallelForEach<TUniform>(World world, TUniform uniform, QueryChainAction_{hash}<TUniform> action)");
        sb.AppendLine("    {");
        sb.AppendLine("        var chunks = new System.Collections.Generic.List<ArchetypeChunk>();");
        sb.AppendLine("        foreach (var chunk in Cached.Resolve(world)) chunks.Add(chunk);");
        sb.AppendLine();
        sb.AppendLine("        System.Threading.Tasks.Parallel.ForEach(chunks, chunk =>");
        var parallelProcessCallArgs = string.Join(", ", new[] { "uniform", "action", "chunk.Count" }.Concat(dataElements.Select(e => $"chunk.Access<{AccessorType(e)}>()")));
        sb.AppendLine($"            Process({parallelProcessCallArgs}));");
        sb.AppendLine();
        var parallelProcessParams = string.Join(", ", new[] { "TUniform uniform", $"QueryChainAction_{hash}<TUniform> action", "int count" }.Concat(dataElements.Select(e => $"{AccessorType(e)} {ParamName(e)}")));
        sb.AppendLine($"        static void Process({parallelProcessParams})");
        sb.AppendLine("        {");
        sb.AppendLine("            for (var i = 0; i < count; i++)");
        var parallelActionCallArgs = string.Join(", ", new[] { "uniform" }.Concat(dataElements.Select(e => $"{RefKind(e)} {ParamName(e)}[i]")));
        sb.AppendLine($"                action({parallelActionCallArgs});");
        sb.AppendLine("        }");
        sb.AppendLine("    }");

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Emits the thin per-exact-shape extension method overload (one call per distinct
    /// <see cref="QueryShape.ExactShapeTypeName"/>) that delegates into the shared
    /// backend <see cref="RenderBackend"/> emits for this shape's
    /// <see cref="QueryShapeExtensions.DedupKey"/>.
    /// </summary>
    internal static string RenderForEachOverload(QueryShape shape)
    {
        var hash = shape.HashName();
        var overloadHash = ExactShapeHash(shape);
        var sb = new StringBuilder();
        sb.AppendLine("using Wyrd.Ecs;");
        sb.AppendLine();
        sb.AppendLine("namespace Wyrd.Ecs;");
        sb.AppendLine();
        sb.AppendLine($"public static class QueryChainTerminals_{overloadHash}");
        sb.AppendLine("{");
        sb.AppendLine($"    public static void ForEach<TUniform>(this {shape.ExactShapeTypeName} query, TUniform uniform, QueryChainAction_{hash}<TUniform> action) =>");
        sb.AppendLine($"        QueryChainWorker_{hash}.RunForEach(query.World, uniform, action);");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>The predicate-delegate `.ForEach` overload — same receiver/grouping rules as <see cref="RenderForEachOverload"/>, see Task 8.</summary>
    internal static string RenderPredicateForEachOverload(QueryShape shape)
    {
        var hash = shape.HashName();
        var overloadHash = ExactShapeHash(shape);
        var sb = new StringBuilder();
        sb.AppendLine("using Wyrd.Ecs;");
        sb.AppendLine();
        sb.AppendLine("namespace Wyrd.Ecs;");
        sb.AppendLine();
        sb.AppendLine($"public static class QueryChainPredicateTerminals_{overloadHash}");
        sb.AppendLine("{");
        sb.AppendLine($"    public static void ForEach<TUniform>(this {shape.ExactShapeTypeName} query, TUniform uniform, QueryChainPredicate_{hash}<TUniform> action) =>");
        sb.AppendLine($"        QueryChainWorker_{hash}.RunForEachPredicate(query.World, uniform, action);");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>The `.ParallelForEach` overload — same receiver/grouping rules as <see cref="RenderForEachOverload"/>, see Task 9.</summary>
    internal static string RenderParallelForEachOverload(QueryShape shape)
    {
        var hash = shape.HashName();
        var overloadHash = ExactShapeHash(shape);
        var sb = new StringBuilder();
        sb.AppendLine("using Wyrd.Ecs;");
        sb.AppendLine();
        sb.AppendLine("namespace Wyrd.Ecs;");
        sb.AppendLine();
        sb.AppendLine($"public static class QueryChainParallelTerminals_{overloadHash}");
        sb.AppendLine("{");
        sb.AppendLine($"    public static void ParallelForEach<TUniform>(this {shape.ExactShapeTypeName} query, TUniform uniform, QueryChainAction_{hash}<TUniform> action) =>");
        sb.AppendLine($"        QueryChainWorker_{hash}.RunParallelForEach(query.World, uniform, action);");
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
