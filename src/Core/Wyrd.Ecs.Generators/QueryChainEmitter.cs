using System.Linq;
using System.Text;

namespace Wyrd.Ecs.Generators;

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

        var ownActionParams = string.Join(", ", new[] { "in TState state" }.Concat(ownElements.Select(ParamDecl)));
        sb.AppendLine($"internal delegate void QueryChainActionOwn_{overloadHash}<TState>({ownActionParams});");
        sb.AppendLine();

        var noUniformActionParams = string.Join(", ", ownElements.Select(ParamDecl));
        sb.AppendLine($"internal delegate void QueryChainAction_{overloadHash}({noUniformActionParams});");
        sb.AppendLine();

        var accessArgs = ownElements.Select(e => $"chunk.Access<{AccessorType(e)}>()").ToList();

        sb.AppendLine($"internal static class QueryChainTerminals_{overloadHash}");
        sb.AppendLine("{");
        sb.AppendLine($"    internal static void ForEach<TState>(this {shape.ExactShapeTypeName} query, in TState state, QueryChainActionOwn_{overloadHash}<TState> action)");
        sb.AppendLine("    {");
        sb.AppendLine($"        foreach (var chunk in QueryChainBackend_{hash}.Cached.Resolve(query.World))");
        var processCallArgs = string.Join(", ", new[] { "state", "action", "chunk.Count" }.Concat(accessArgs));
        sb.AppendLine($"            Process({processCallArgs});");
        sb.AppendLine();
        var processParams = string.Join(", ", new[] { "in TState state", $"QueryChainActionOwn_{overloadHash}<TState> action", "int count" }.Concat(ownElements.Select(e => $"{AccessorType(e)} {ParamName(e)}")));
        sb.AppendLine($"        static void Process({processParams})");
        sb.AppendLine("        {");
        sb.AppendLine("            for (var i = 0; i < count; i++)");
        var actionCallArgs = string.Join(", ", new[] { "state" }.Concat(ownElements.Select(e => $"{RefKind(e)} {ParamName(e)}[i]")));
        sb.AppendLine($"                action({actionCallArgs});");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    internal static void ForEach(this {shape.ExactShapeTypeName} query, QueryChainAction_{overloadHash} action)");
        sb.AppendLine("    {");
        sb.AppendLine($"        foreach (var chunk in QueryChainBackend_{hash}.Cached.Resolve(query.World))");
        var noUniformProcessCallArgs = string.Join(", ", new[] { "action", "chunk.Count" }.Concat(accessArgs));
        sb.AppendLine($"            Process({noUniformProcessCallArgs});");
        sb.AppendLine();
        var noUniformProcessParams = string.Join(", ", new[] { $"QueryChainAction_{overloadHash} action", "int count" }.Concat(ownElements.Select(e => $"{AccessorType(e)} {ParamName(e)}")));
        sb.AppendLine($"        static void Process({noUniformProcessParams})");
        sb.AppendLine("        {");
        sb.AppendLine("            for (var i = 0; i < count; i++)");
        var noUniformActionCallArgs = string.Join(", ", ownElements.Select(e => $"{RefKind(e)} {ParamName(e)}[i]"));
        sb.AppendLine($"                action({noUniformActionCallArgs});");
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

        var ownActionParams = string.Join(", ", new[] { "in TState state" }.Concat(ownElements.Select(ParamDecl)));
        sb.AppendLine($"internal delegate bool QueryChainPredicateOwn_{overloadHash}<TState>({ownActionParams});");
        sb.AppendLine();

        var noUniformPredicateParams = string.Join(", ", ownElements.Select(ParamDecl));
        sb.AppendLine($"internal delegate bool QueryChainPredicate_{overloadHash}({noUniformPredicateParams});");
        sb.AppendLine();

        var accessArgs = ownElements.Select(e => $"chunk.Access<{AccessorType(e)}>()").ToList();

        sb.AppendLine($"internal static class QueryChainPredicateTerminals_{overloadHash}");
        sb.AppendLine("{");
        sb.AppendLine($"    internal static void ForEach<TState>(this {shape.ExactShapeTypeName} query, in TState state, QueryChainPredicateOwn_{overloadHash}<TState> action)");
        sb.AppendLine("    {");
        sb.AppendLine($"        foreach (var chunk in QueryChainBackend_{hash}.Cached.Resolve(query.World))");
        var predicateProcessCallArgs = string.Join(", ", new[] { "state", "action", "chunk.Count" }.Concat(accessArgs));
        sb.AppendLine($"            if (!Process({predicateProcessCallArgs})) return;");
        sb.AppendLine();
        var predicateProcessParams = string.Join(", ", new[] { "in TState state", $"QueryChainPredicateOwn_{overloadHash}<TState> action", "int count" }.Concat(ownElements.Select(e => $"{AccessorType(e)} {ParamName(e)}")));
        sb.AppendLine($"        static bool Process({predicateProcessParams})");
        sb.AppendLine("        {");
        sb.AppendLine("            for (var i = 0; i < count; i++)");
        var predicateActionCallArgs = string.Join(", ", new[] { "state" }.Concat(ownElements.Select(e => $"{RefKind(e)} {ParamName(e)}[i]")));
        sb.AppendLine($"                if (!action({predicateActionCallArgs})) return false;");
        sb.AppendLine("            return true;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    internal static void ForEach(this {shape.ExactShapeTypeName} query, QueryChainPredicate_{overloadHash} action)");
        sb.AppendLine("    {");
        sb.AppendLine($"        foreach (var chunk in QueryChainBackend_{hash}.Cached.Resolve(query.World))");
        var noUniformPredicateProcessCallArgs = string.Join(", ", new[] { "action", "chunk.Count" }.Concat(accessArgs));
        sb.AppendLine($"            if (!Process({noUniformPredicateProcessCallArgs})) return;");
        sb.AppendLine();
        var noUniformPredicateProcessParams = string.Join(", ", new[] { $"QueryChainPredicate_{overloadHash} action", "int count" }.Concat(ownElements.Select(e => $"{AccessorType(e)} {ParamName(e)}")));
        sb.AppendLine($"        static bool Process({noUniformPredicateProcessParams})");
        sb.AppendLine("        {");
        sb.AppendLine("            for (var i = 0; i < count; i++)");
        var noUniformPredicateActionCallArgs = string.Join(", ", ownElements.Select(e => $"{RefKind(e)} {ParamName(e)}[i]"));
        sb.AppendLine($"                if (!action({noUniformPredicateActionCallArgs})) return false;");
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

        var accessArgs = ownElements.Select(e => $"chunk.Access<{AccessorType(e)}>()").ToList();

        sb.AppendLine($"internal static class QueryChainParallelTerminals_{overloadHash}");
        sb.AppendLine("{");
        sb.AppendLine($"    internal static void ParallelForEach<TState>(this {shape.ExactShapeTypeName} query, in TState state, QueryChainActionOwn_{overloadHash}<TState> action)");
        sb.AppendLine("    {");
        sb.AppendLine("        var chunks = new System.Collections.Generic.List<ArchetypeChunk>();");
        sb.AppendLine($"        foreach (var chunk in QueryChainBackend_{hash}.Cached.Resolve(query.World)) chunks.Add(chunk);");
        sb.AppendLine();
        // An `in` parameter can't be captured by a closure (CS1628) -- copy it to an
        // ordinary local first. One copy per `.ParallelForEach()` call, not per entity,
        // so it doesn't undermine the no-per-entity-allocation point of `in` at all.
        sb.AppendLine("        var capturedState = state;");
        sb.AppendLine("        System.Threading.Tasks.Parallel.ForEach(chunks, chunk =>");
        var parallelProcessCallArgs = string.Join(", ", new[] { "capturedState", "action", "chunk.Count" }.Concat(accessArgs));
        sb.AppendLine($"            Process({parallelProcessCallArgs}));");
        sb.AppendLine();
        var parallelProcessParams = string.Join(", ", new[] { "in TState state", $"QueryChainActionOwn_{overloadHash}<TState> action", "int count" }.Concat(ownElements.Select(e => $"{AccessorType(e)} {ParamName(e)}")));
        sb.AppendLine($"        static void Process({parallelProcessParams})");
        sb.AppendLine("        {");
        sb.AppendLine("            for (var i = 0; i < count; i++)");
        var parallelActionCallArgs = string.Join(", ", new[] { "state" }.Concat(ownElements.Select(e => $"{RefKind(e)} {ParamName(e)}[i]")));
        sb.AppendLine($"                action({parallelActionCallArgs});");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    internal static void ParallelForEach(this {shape.ExactShapeTypeName} query, QueryChainAction_{overloadHash} action)");
        sb.AppendLine("    {");
        sb.AppendLine("        var chunks = new System.Collections.Generic.List<ArchetypeChunk>();");
        sb.AppendLine($"        foreach (var chunk in QueryChainBackend_{hash}.Cached.Resolve(query.World)) chunks.Add(chunk);");
        sb.AppendLine();
        sb.AppendLine("        System.Threading.Tasks.Parallel.ForEach(chunks, chunk =>");
        var noUniformParallelProcessCallArgs = string.Join(", ", new[] { "action", "chunk.Count" }.Concat(accessArgs));
        sb.AppendLine($"            Process({noUniformParallelProcessCallArgs}));");
        sb.AppendLine();
        var noUniformParallelProcessParams = string.Join(", ", new[] { $"QueryChainAction_{overloadHash} action", "int count" }.Concat(ownElements.Select(e => $"{AccessorType(e)} {ParamName(e)}")));
        sb.AppendLine($"        static void Process({noUniformParallelProcessParams})");
        sb.AppendLine("        {");
        sb.AppendLine("            for (var i = 0; i < count; i++)");
        var noUniformParallelActionCallArgs = string.Join(", ", ownElements.Select(e => $"{RefKind(e)} {ParamName(e)}[i]"));
        sb.AppendLine($"                action({noUniformParallelActionCallArgs});");
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
    /// Emits `WithSystems` sugar so a consumer never has to spell out
    /// `Wyrd.Ecs.Generated.GeneratedSystemAccess.Entries` by hand: a `params EcsSystem[]`
    /// overload for constructor-arg systems, plus `WithSystems&lt;T0..T{ArityCap.Max}&gt;()`
    /// for the parameterless case. Emitted into namespace `Wyrd.Ecs` itself, not
    /// `Wyrd.Ecs.Generated` — every consumer already has `using Wyrd.Ecs;` in scope for
    /// `WorldBuilder`/`EcsSystem`, so the extension methods are visible without a second
    /// `using` just for them. Unconditional — this doesn't depend on any discovered
    /// `QuerySystem`/chain candidate, so it's the same fixed output regardless of what a
    /// consumer's own compilation contains.
    /// </summary>
    internal static string RenderWithSystemsExtensions()
    {
        var sb = new StringBuilder();
        sb.AppendLine("namespace Wyrd.Ecs;");
        sb.AppendLine();
        sb.AppendLine("public static class GeneratedWorldBuilderExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    public static WorldBuilder WithSystems(this WorldBuilder builder, params EcsSystem[] systems) =>");
        sb.AppendLine("        builder.WithSystems(Wyrd.Ecs.Generated.GeneratedSystemAccess.Entries, systems);");
        sb.AppendLine();

        for (var n = 1; n <= ArityCap.Max; n++)
        {
            var typeParams = string.Join(", ", Enumerable.Range(0, n).Select(i => $"T{i}"));
            var constraints = string.Join(" ", Enumerable.Range(0, n).Select(i => $"where T{i} : EcsSystem, new()"));
            var instances = string.Join(", ", Enumerable.Range(0, n).Select(i => $"new T{i}()"));

            sb.AppendLine($"    public static WorldBuilder WithSystems<{typeParams}>(this WorldBuilder builder) {constraints} =>");
            sb.AppendLine($"        builder.WithSystems(Wyrd.Ecs.Generated.GeneratedSystemAccess.Entries, {instances});");
            sb.AppendLine();
        }

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
        // OwnDataElements(), not DataElements() -- this is a caller-facing parameter
        // list (both Execute's own declaration and the lambda passed to .ForEach, which
        // must match that terminal's own OwnDataElements()-ordered delegate type), and
        // DataElements()'s own doc comment says exactly that: "Not used for any
        // caller-facing parameter list." Every existing QuerySystem test happened to
        // have alphabetical-by-type-name order match declaration order, which is why
        // this went uncaught until a three-component shape where they diverge.
        var dataElements = candidate.Shape.OwnDataElements();
        var executeParams = string.Join(", ", new[] { "Time time" }.Concat(dataElements.Select(ParamDecl)));
        // Calling a ref/in parameter requires the same modifier at the call site, not
        // just on the parameter declaration -- RefKind(e) here, not a bare ParamName(e).
        // Both lists are built by prepending "in Time t"/"Time time" into the *same*
        // list before joining (matching RenderBackend's actionParams pattern), not by
        // joining dataElements alone and string-concatenating a separator afterward --
        // the latter produces a trailing comma ("(in Time t, )") when dataElements is
        // empty (a filter-only shape with no Writes/Reads at all), which doesn't compile.
        // The lambda's own first parameter needs "in" to match ForEach<TState>'s now-`in
        // TState state` delegate parameter (QueryChainActionOwn_<hash><TState>) -- Execute's
        // own declared "Time time" above doesn't, since it's called with a plain by-value
        // copy of `t` from inside the lambda body, not required to repeat the modifier.
        var lambdaParams = string.Join(", ", new[] { "in Time t" }.Concat(dataElements.Select(ParamDecl)));
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
        sb.AppendLine("    protected override void OnUpdate(World world, Time time) =>");
        sb.AppendLine($"        (({candidate.Shape.ExactShapeTypeName})Build(world)).ForEach(time, ({lambdaParams}) => Execute({executeCallArgs}));");
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
