using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Wyrd.Ecs.Generators;

internal static class QueryChainEmitter
{
    /// <summary>
    /// Emits the shared backend for one *logical* shape (one call per distinct
    /// <see cref="QueryShapeExtensions.DedupKey"/> among the shapes passed to
    /// <see cref="RenderForEachOverload"/>): the cached <c>ArchetypeQuery</c> covering just
    /// this shape's statically-known accessor requirements (one <c>.Access&lt;TAccessor&gt;()</c>
    /// per Reads/Writes marker), the bespoke delegate type, and the actual per-chunk
    /// iteration logic. Reused by every exact-declaration-order overload sharing this shape.
    /// <c>.Without</c>/<c>.Has</c>/<c>.Any</c> are never baked in here anymore -- they live on
    /// the caller's own <c>Query&lt;TShape&gt;.Filter</c> and get combined in at resolve time
    /// (see <see cref="AppendMethod"/>), which is also why two shapes differing only by which
    /// <c>.Without</c>/<c>.Has</c>/<c>.Any</c> calls they chained now correctly share one
    /// backend instead of getting separate ones.
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
            sb.AppendLine($"        query = query.Access<{AccessorType(m)}>();");
        sb.AppendLine("        return query;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// One generated terminal's shape: which delegate names it uses, what its shared
    /// <c>Process</c> local function returns, and whether it dispatches sequentially or
    /// via <c>Parallel.ForEach</c>. Drives <see cref="AppendTerminalClass"/> so
    /// <see cref="RenderForEachOverload"/>, <see cref="RenderPredicateForEachOverload"/>,
    /// and <see cref="RenderParallelForEachOverload"/> share one implementation instead of
    /// three hand-copied ~60-line methods. Only three instances are ever constructed
    /// (below) -- a predicate-parallel combination is never emitted.
    /// </summary>
    private readonly record struct TerminalSpec(
        string ClassSuffix,
        string MethodName,
        string OwnDelegateName,
        string NoUniformDelegateName,
        string ProcessReturnType,
        bool IsParallel)
    {
        internal static TerminalSpec Action(string overloadHash) => new(
            ClassSuffix: "Terminals",
            MethodName: "ForEach",
            OwnDelegateName: $"QueryChainActionOwn_{overloadHash}",
            NoUniformDelegateName: $"QueryChainAction_{overloadHash}",
            ProcessReturnType: "void",
            IsParallel: false);

        internal static TerminalSpec Predicate(string overloadHash) => new(
            ClassSuffix: "PredicateTerminals",
            MethodName: "ForEach",
            OwnDelegateName: $"QueryChainPredicateOwn_{overloadHash}",
            NoUniformDelegateName: $"QueryChainPredicate_{overloadHash}",
            ProcessReturnType: "bool",
            IsParallel: false);

        // Reuses Action's delegate names -- .ParallelForEach shares the plain .ForEach's
        // per-entity signature (only dispatch differs), so it declares no delegates of
        // its own; see RenderParallelForEachOverload.
        internal static TerminalSpec Parallel(string overloadHash) => new(
            ClassSuffix: "ParallelTerminals",
            MethodName: "ParallelForEach",
            OwnDelegateName: $"QueryChainActionOwn_{overloadHash}",
            NoUniformDelegateName: $"QueryChainAction_{overloadHash}",
            ProcessReturnType: "void",
            IsParallel: true);
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
        var sb = new StringBuilder();
        AppendHeader(sb);
        var spec = TerminalSpec.Action(ExactShapeHash(shape));
        AppendDelegates(sb, shape, spec);
        AppendTerminalClass(sb, shape, spec);
        return sb.ToString();
    }

    /// <summary>The predicate-delegate `.ForEach` overload — same own-order/adapter rules as <see cref="RenderForEachOverload"/>.</summary>
    internal static string RenderPredicateForEachOverload(QueryShape shape)
    {
        var sb = new StringBuilder();
        AppendHeader(sb);
        var spec = TerminalSpec.Predicate(ExactShapeHash(shape));
        AppendDelegates(sb, shape, spec);
        AppendTerminalClass(sb, shape, spec);
        return sb.ToString();
    }

    /// <summary>The `.ParallelForEach` overload — same own-order/adapter rules as <see cref="RenderForEachOverload"/>. Declares no delegates of its own; see <see cref="TerminalSpec.Parallel"/>.</summary>
    internal static string RenderParallelForEachOverload(QueryShape shape)
    {
        var sb = new StringBuilder();
        AppendHeader(sb);
        AppendTerminalClass(sb, shape, TerminalSpec.Parallel(ExactShapeHash(shape)));
        return sb.ToString();
    }

    private static void AppendHeader(StringBuilder sb)
    {
        sb.AppendLine("using Wyrd.Ecs;");
        sb.AppendLine();
        sb.AppendLine("namespace Wyrd.Ecs;");
        sb.AppendLine();
    }

    /// <summary>Declares <paramref name="spec"/>'s own/no-uniform delegate pair. Not called for <see cref="TerminalSpec.Parallel"/> -- it reuses <see cref="TerminalSpec.Action"/>'s delegates, declared by whichever <see cref="RenderForEachOverload"/> call ran for this shape.</summary>
    private static void AppendDelegates(StringBuilder sb, QueryShape shape, TerminalSpec spec)
    {
        var ownElements = shape.OwnDataElements();

        var ownParams = string.Join(", ", new[] { "in TState state" }.Concat(ownElements.Select(ParamDecl)));
        sb.AppendLine($"internal delegate {spec.ProcessReturnType} {spec.OwnDelegateName}<TState>({ownParams});");
        sb.AppendLine();

        var noUniformParams = string.Join(", ", ownElements.Select(ParamDecl));
        sb.AppendLine($"internal delegate {spec.ProcessReturnType} {spec.NoUniformDelegateName}({noUniformParams});");
        sb.AppendLine();
    }

    private static void AppendTerminalClass(StringBuilder sb, QueryShape shape, TerminalSpec spec)
    {
        var ownElements = shape.OwnDataElements();
        var accessArgs = ownElements.Select(e => $"chunk.Access<{AccessorType(e)}>()").ToList();

        sb.AppendLine($"internal static class QueryChain{spec.ClassSuffix}_{ExactShapeHash(shape)}");
        sb.AppendLine("{");
        AppendMethod(sb, shape, spec, ownElements, accessArgs, uniform: true);
        sb.AppendLine();
        AppendMethod(sb, shape, spec, ownElements, accessArgs, uniform: false);
        sb.AppendLine("}");
    }

    /// <summary>
    /// Emits one of <paramref name="spec"/>'s two overloads (<paramref name="uniform"/>
    /// selects the state-carrying vs. plain form) -- the outer extension method plus its
    /// local <c>Process</c> function. The outer method is always <c>void</c>; only
    /// <c>Process</c>'s return type and early-exit behavior vary with
    /// <see cref="TerminalSpec.ProcessReturnType"/>, and only the chunk-gathering
    /// preamble varies with <see cref="TerminalSpec.IsParallel"/>.
    /// </summary>
    private static void AppendMethod(
        StringBuilder sb,
        QueryShape shape,
        TerminalSpec spec,
        ImmutableArray<MarkerElement> ownElements,
        List<string> accessArgs,
        bool uniform)
    {
        var hash = shape.HashName();
        var typeParam = uniform ? "<TState>" : "";
        var actionParamDecl = uniform
            ? $"in TState state, {spec.OwnDelegateName}<TState> action"
            : $"{spec.NoUniformDelegateName} action";

        sb.AppendLine($"    internal static void {spec.MethodName}{typeParam}(this {shape.ExactShapeTypeName} query, {actionParamDecl})");
        sb.AppendLine("    {");

        if (spec.IsParallel)
        {
            sb.AppendLine("        var chunks = new System.Collections.Generic.List<ArchetypeChunk>();");
            sb.AppendLine($"        foreach (var chunk in QueryChainBackend_{hash}.Cached.Combine(query.Filter).Resolve(query.World)) chunks.Add(chunk);");
            sb.AppendLine();
            if (uniform)
            {
                // An `in` parameter can't be captured by a closure (CS1628) -- copy it to
                // an ordinary local first. One copy per `.ParallelForEach()` call, not per
                // entity, so it doesn't undermine the no-per-entity-allocation point of
                // `in` at all.
                sb.AppendLine("        var capturedState = state;");
            }
            sb.AppendLine("        System.Threading.Tasks.Parallel.ForEach(chunks, chunk =>");
            var leading = uniform ? new[] { "capturedState", "action", "chunk.Count" } : ["action", "chunk.Count"];
            var callArgs = string.Join(", ", leading.Concat(accessArgs));
            sb.AppendLine($"            Process({callArgs}));");
        }
        else
        {
            sb.AppendLine($"        foreach (var chunk in QueryChainBackend_{hash}.Cached.Combine(query.Filter).Resolve(query.World))");
            var leading = uniform ? new[] { "state", "action", "chunk.Count" } : ["action", "chunk.Count"];
            var callArgs = string.Join(", ", leading.Concat(accessArgs));
            var callStatement = spec.ProcessReturnType == "bool" ? $"if (!Process({callArgs})) return;" : $"Process({callArgs});";
            sb.AppendLine($"            {callStatement}");
        }

        sb.AppendLine();

        var processLeading = uniform
            ? new[] { "in TState state", $"{spec.OwnDelegateName}<TState> action", "int count" }
            : [$"{spec.NoUniformDelegateName} action", "int count"];
        var processParams = string.Join(", ", processLeading.Concat(ownElements.Select(e => $"{AccessorType(e)} {ParamName(e)}")));
        sb.AppendLine($"        static {spec.ProcessReturnType} Process({processParams})");
        sb.AppendLine("        {");
        sb.AppendLine("            for (var i = 0; i < count; i++)");

        var actionLeading = uniform ? new[] { "state" } : System.Array.Empty<string>();
        var actionCallArgs = string.Join(", ", actionLeading.Concat(ownElements.Select(e => $"{RefKind(e)} {ParamName(e)}[i]")));
        if (spec.ProcessReturnType == "bool")
        {
            sb.AppendLine($"                if (!action({actionCallArgs})) return false;");
            sb.AppendLine("            return true;");
        }
        else
        {
            sb.AppendLine($"                action({actionCallArgs});");
        }

        sb.AppendLine("        }");
        sb.AppendLine("    }");
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
        sb.AppendLine("    public static WorldBuilder WithSystems(this WorldBuilder builder, params OrderedSystem[] systems) =>");
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
    /// Emits the `partial` class part supplying a `QuerySystem` subclass's
    /// `EcsSystem.Execute` implementation, calling the developer-written `Update` method
    /// (an ordinary method, not `partial` — its own `ref`/`in` modifiers are the source of
    /// truth for access mode, read by <c>QueryChainGenerator.TryExtractQuerySystem</c>, so
    /// there is nothing left for this class to pre-declare).
    /// </summary>
    internal static string RenderQuerySystemGlue(QuerySystemCandidate candidate)
    {
        // OwnDataElements(), not DataElements() -- this is a caller-facing parameter
        // list (both Update's own declaration and the lambda passed to .ForEach, which
        // must match that terminal's own OwnDataElements()-ordered delegate type), and
        // DataElements()'s own doc comment says exactly that: "Not used for any
        // caller-facing parameter list." Every existing QuerySystem test happened to
        // have alphabetical-by-type-name order match declaration order, which is why
        // this went uncaught until a three-component shape where they diverge.
        var dataElements = candidate.Shape.OwnDataElements();
        // Calling a ref/in parameter requires the same modifier at the call site, not
        // just on the parameter declaration -- RefKind(e) here, not a bare ParamName(e).
        // Built by prepending "in Time t" into the same list before joining (matching
        // RenderBackend's actionParams pattern), not by joining dataElements alone and
        // string-concatenating a separator afterward -- the latter produces a trailing
        // comma ("(in Time t, )") when dataElements is empty (a filter-only shape with no
        // Writes/Reads at all), which doesn't compile. The lambda's own first parameter
        // needs "in" to match ForEach<TState>'s now-`in TState state` delegate parameter
        // (QueryChainActionOwn_<hash><TState>).
        var lambdaParams = string.Join(", ", new[] { "in Time t" }.Concat(dataElements.Select(ParamDecl)));
        var updateCallArgs = string.Join(", ", new[] { "t" }.Concat(dataElements.Select(e => $"{RefKind(e)} {ParamName(e)}")));

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
        sb.AppendLine("    protected override void Execute(World world, Time time) =>");
        sb.AppendLine($"        (({candidate.Shape.ExactShapeTypeName})DefineQuery(world)).ForEach(time, ({lambdaParams}) => Update({updateCallArgs}));");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// A stable, valid-C#-identifier suffix derived from <see cref="QueryShape.ExactShapeTypeName"/>
    /// *and* <see cref="QueryShape.Markers"/> — distinct from <see cref="QueryShapeExtensions.HashName"/>,
    /// which is derived from the order-independent <see cref="QueryShapeExtensions.DedupKey"/>
    /// instead. Markers must be folded in here, not just the type name: since
    /// <c>.Without</c>/<c>.Has</c>/<c>.Any</c> no longer affect <c>TShape</c>, two shapes can
    /// share one <see cref="QueryShape.ExactShapeTypeName"/> while resolving different ref/in
    /// markers for the same component (see <c>QueryChainGenerator.DeduplicateShapes</c>'s own
    /// doc comment) — hashing the type name alone would collide their generated class names.
    /// </summary>
    internal static string ExactShapeHash(QueryShape shape)
    {
        var hash = 2166136261u;
        foreach (var c in shape.ExactShapeTypeName)
        {
            hash ^= c;
            hash *= 16777619u;
        }
        foreach (var m in shape.Markers)
        {
            foreach (var c in $"|{m.Kind}:{m.ComponentTypeName}")
            {
                hash ^= c;
                hash *= 16777619u;
            }
        }
        return hash.ToString("x8");
    }

    private static string AccessorType(MarkerElement e) => $"{(e.Kind == MarkerKind.Writes ? "Mut" : "Ref")}<{e.ComponentTypeName}>";
    private static string RefKind(MarkerElement e) => e.Kind == MarkerKind.Writes ? "ref" : "in";
    private static string ParamDecl(MarkerElement e) => $"{RefKind(e)} {e.ComponentTypeName} {ParamName(e)}";

    private static string ParamName(MarkerElement e)
    {
        var name = e.ComponentTypeName;
        // Namespace-strip only the outer type's own name, not the last '.' anywhere in the
        // fully-qualified name -- for a generic component type (e.g. RelationLinks<Ns.Foo>),
        // the last '.' can sit inside a generic argument's namespace, which previously
        // produced a garbage identifier (including the argument's own '>').
        var genericStart = name.IndexOf('<');
        var outerTypeName = genericStart >= 0 ? name[..genericStart] : name;
        var simple = outerTypeName.Contains('.') ? outerTypeName[(outerTypeName.LastIndexOf('.') + 1)..] : outerTypeName;
        return char.ToLowerInvariant(simple[0]) + simple[1..];
    }
}
