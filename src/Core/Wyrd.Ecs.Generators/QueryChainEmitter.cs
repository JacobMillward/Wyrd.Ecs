using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Wyrd.Ecs.Generators;

internal static class QueryChainEmitter
{
    /// <summary>
    /// Emits the shared backend for one *logical* shape (one call per distinct
    /// <see cref="QueryShapeExtensions.DedupKey"/>): the cached <c>ArchetypeQuery</c>
    /// covering this shape's accessor requirements, the delegate type, and the per-chunk
    /// iteration logic. Reused by every exact-declaration-order overload sharing this
    /// shape. <c>.Without</c>/<c>.Has</c>/<c>.Any</c> are never baked in here: they live
    /// on the caller's own <c>Query&lt;TShape&gt;.Filter</c> and combine in at resolve
    /// time (see <see cref="AppendMethod"/>), so two shapes differing only by those calls
    /// share one backend.
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
    /// via <c>Parallel.ForEach</c>. Drives <see cref="AppendTerminalClass"/> so the three
    /// ForEach/PredicateForEach/ParallelForEach renderers share one implementation. Only
    /// three instances are ever constructed: a predicate-parallel combination is never
    /// emitted.
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

        // Reuses Action's delegate names: .ParallelForEach shares the plain .ForEach's
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
    /// <see cref="QueryShape.ExactShapeTypeName"/>). Its public delegate/parameter list
    /// uses this shape's own declaration order, not the shared backend's alphabetical
    /// order; the body adapts between the two when calling into the backend, so a caller
    /// never needs to match the backend's internal ordering.
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

    /// <summary>The predicate-delegate `.ForEach` overload. Same own-order/adapter rules as <see cref="RenderForEachOverload"/>.</summary>
    internal static string RenderPredicateForEachOverload(QueryShape shape)
    {
        var sb = new StringBuilder();
        AppendHeader(sb);
        var spec = TerminalSpec.Predicate(ExactShapeHash(shape));
        AppendDelegates(sb, shape, spec);
        AppendTerminalClass(sb, shape, spec);
        return sb.ToString();
    }

    /// <summary>The `.ParallelForEach` overload. Same own-order/adapter rules as <see cref="RenderForEachOverload"/>. Declares no delegates of its own; see <see cref="TerminalSpec.Parallel"/>.</summary>
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

    /// <summary>Declares <paramref name="spec"/>'s own/no-uniform delegate pair. Not called for <see cref="TerminalSpec.Parallel"/>: it reuses <see cref="TerminalSpec.Action"/>'s delegates, declared by whichever <see cref="RenderForEachOverload"/> call ran for this shape.</summary>
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
    /// selects the state-carrying vs. plain form): the outer extension method plus its
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
            sb.AppendLine($"        QueryChainBackend_{hash}.Cached.Resolve(query.World, query.Filter).CollectParallelChunks(chunks);");
            sb.AppendLine();
            if (uniform)
            {
                // An `in` parameter can't be captured by a closure (CS1628), so copy it to
                // an ordinary local first. One copy per `.ParallelForEach()` call, not per
                // entity, so it doesn't undermine the no-per-entity-allocation point of `in`.
                sb.AppendLine("        var capturedState = state;");
            }
            sb.AppendLine("        System.Threading.Tasks.Parallel.ForEach(chunks, chunk =>");
            var leading = uniform ? new[] { "capturedState", "action", "chunk.Count" } : ["action", "chunk.Count"];
            var callArgs = string.Join(", ", leading.Concat(accessArgs));
            sb.AppendLine($"            Process({callArgs}));");
        }
        else
        {
            sb.AppendLine($"        foreach (var chunk in QueryChainBackend_{hash}.Cached.Resolve(query.World, query.Filter))");
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

    /// <summary>Emits the <c>SystemRegistry</c> registry the static-parallel-scheduler plan's scheduler consumes.</summary>
    internal static string RenderSystemAccessRegistry(
        IReadOnlyList<(string SystemTypeName, List<string> Reads, List<string> Writes)> systems,
        IReadOnlyList<(string SystemTypeName, List<string> Before, List<string> After)> edges,
        IReadOnlyList<string> fixedTimestepSystemTypeNames,
        IReadOnlyList<(string SystemTypeName, bool TakesWorld, ImmutableArray<QueryChainGenerator.ResourceParameter> Resources)> constructors)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using Wyrd.Ecs;");
        sb.AppendLine();
        sb.AppendLine("namespace Wyrd.Ecs.Generated;");
        sb.AppendLine();
        // Generated members are never hand-authored, so a consumer project with
        // GenerateDocumentationFile enabled would otherwise see CS1591 (missing XML doc)
        // for every member below.
        sb.AppendLine("#pragma warning disable CS1591");
        sb.AppendLine("internal static class SystemRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    internal static readonly IReadOnlyDictionary<Type, SystemAccess> Access = new Dictionary<Type, SystemAccess>");
        sb.AppendLine("    {");
        foreach (var system in systems)
        {
            var reads = string.Join(", ", system.Reads.Select(t => $"typeof({t})"));
            var writes = string.Join(", ", system.Writes.Select(t => $"typeof({t})"));
            sb.AppendLine($"        [typeof(global::{system.SystemTypeName})] = new(Reads: new Type[] {{ {reads} }}, Writes: new Type[] {{ {writes} }}),");
        }
        sb.AppendLine("    };");
        sb.AppendLine();
        sb.AppendLine("    internal static readonly IReadOnlyDictionary<Type, (IReadOnlyList<Type> Before, IReadOnlyList<Type> After)> Edges = new Dictionary<Type, (IReadOnlyList<Type>, IReadOnlyList<Type>)>");
        sb.AppendLine("    {");
        foreach (var edge in edges)
        {
            var before = string.Join(", ", edge.Before.Select(t => $"typeof(global::{t})"));
            var after = string.Join(", ", edge.After.Select(t => $"typeof(global::{t})"));
            sb.AppendLine($"        [typeof(global::{edge.SystemTypeName})] = (new Type[] {{ {before} }}, new Type[] {{ {after} }}),");
        }
        sb.AppendLine("    };");
        sb.AppendLine();
        sb.AppendLine("    internal static readonly IReadOnlyDictionary<Type, SystemCadence> Cadence = new Dictionary<Type, SystemCadence>");
        sb.AppendLine("    {");
        foreach (var systemTypeName in fixedTimestepSystemTypeNames)
            sb.AppendLine($"        [typeof(global::{systemTypeName})] = SystemCadence.Fixed,");
        sb.AppendLine("    };");
        sb.AppendLine();
        sb.AppendLine("    internal static readonly IReadOnlyDictionary<Type, Func<World, EcsSystem>> Construct = new Dictionary<Type, Func<World, EcsSystem>>");
        sb.AppendLine("    {");
        foreach (var ctor in constructors)
        {
            var args = new List<string>();
            if (ctor.TakesWorld) args.Add("world");
            foreach (var resource in ctor.Resources)
                args.Add(resource.IsWrite ? $"ref world.GetResourceRef<{resource.ResourceTypeName}>()" : $"world.GetResource<{resource.ResourceTypeName}>()");
            var factory = $"world => new global::{ctor.SystemTypeName}({string.Join(", ", args)})";
            sb.AppendLine($"        [typeof(global::{ctor.SystemTypeName})] = {factory},");
        }
        sb.AppendLine("    };");
        sb.AppendLine("}");
        sb.AppendLine("#pragma warning restore CS1591");
        return sb.ToString();
    }

    /// <summary>
    /// Emits `AddSystem&lt;T&gt;()` overloads on `WorldBuilder`, `World`, and
    /// `SystemRegistration` so a consumer never spells out `Wyrd.Ecs.Generated.SystemRegistry`
    /// by hand, and a fluent chain keeps working regardless of which one the previous call
    /// returned. `Access`/`Cadence` degrade gracefully to `null`/`SystemCadence.Variable` via
    /// `AccessOrNull`/`CadenceOrDefault` for a system with no generated footprint or no
    /// `[FixedTimestep]` attribute. `Edges` seeds the entry via `EdgesOrEmpty` before any
    /// `.Before&lt;T&gt;()`/`.After&lt;T&gt;()` chained afterward unions in more. `Construct`
    /// uses the plain indexer instead: a type with no generator entry (see
    /// `QueryChainGenerator.ExtractConstructorShape`) throws `KeyNotFoundException` here; use
    /// the `Func&lt;World, T&gt;` overload for that type instead. Unconditional, fixed-shape
    /// emission: these methods are generic over any `T : EcsSystem`, so no iteration over
    /// discovered candidates is needed.
    /// </summary>
    internal static string RenderAddSystemExtensions()
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine("namespace Wyrd.Ecs;");
        sb.AppendLine();
        // Same rationale as RenderSystemAccessRegistry's pragma above: this is generated,
        // never hand-authored, so it shouldn't trigger a consumer's CS1591.
        sb.AppendLine("#pragma warning disable CS1591");
        sb.AppendLine("internal static class GeneratedSystemRegistrationExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    internal static SystemRegistration AddSystem<T>(this WorldBuilder builder) where T : EcsSystem =>");
        sb.AppendLine("        builder.AddSystemCore(typeof(T), AccessOrNull(typeof(T)), Wyrd.Ecs.Generated.SystemRegistry.Construct[typeof(T)], EdgesOrEmpty(typeof(T)).Before, EdgesOrEmpty(typeof(T)).After, CadenceOrDefault(typeof(T)));");
        sb.AppendLine();
        sb.AppendLine("    internal static SystemRegistration AddSystem<T>(this WorldBuilder builder, Func<World, T> configure) where T : EcsSystem =>");
        sb.AppendLine("        builder.AddSystemCore(typeof(T), AccessOrNull(typeof(T)), w => configure(w), EdgesOrEmpty(typeof(T)).Before, EdgesOrEmpty(typeof(T)).After, CadenceOrDefault(typeof(T)));");
        sb.AppendLine();
        sb.AppendLine("    internal static SystemRegistration AddSystem<T>(this SystemRegistration registration) where T : EcsSystem =>");
        sb.AppendLine("        registration.RegisterNext(typeof(T), AccessOrNull(typeof(T)), Wyrd.Ecs.Generated.SystemRegistry.Construct[typeof(T)], EdgesOrEmpty(typeof(T)).Before, EdgesOrEmpty(typeof(T)).After, CadenceOrDefault(typeof(T)));");
        sb.AppendLine();
        sb.AppendLine("    internal static SystemRegistration AddSystem<T>(this SystemRegistration registration, Func<World, T> configure) where T : EcsSystem =>");
        sb.AppendLine("        registration.RegisterNext(typeof(T), AccessOrNull(typeof(T)), w => configure(w), EdgesOrEmpty(typeof(T)).Before, EdgesOrEmpty(typeof(T)).After, CadenceOrDefault(typeof(T)));");
        sb.AppendLine();
        sb.AppendLine("    internal static SystemRegistration AddSystem<T>(this World world) where T : EcsSystem =>");
        sb.AppendLine("        world.AddSystemCore(typeof(T), AccessOrNull(typeof(T)), Wyrd.Ecs.Generated.SystemRegistry.Construct[typeof(T)], EdgesOrEmpty(typeof(T)).Before, EdgesOrEmpty(typeof(T)).After, CadenceOrDefault(typeof(T)));");
        sb.AppendLine();
        sb.AppendLine("    internal static SystemRegistration AddSystem<T>(this World world, Func<World, T> configure) where T : EcsSystem =>");
        sb.AppendLine("        world.AddSystemCore(typeof(T), AccessOrNull(typeof(T)), w => configure(w), EdgesOrEmpty(typeof(T)).Before, EdgesOrEmpty(typeof(T)).After, CadenceOrDefault(typeof(T)));");
        sb.AppendLine();
        sb.AppendLine("    private static SystemAccess? AccessOrNull(Type systemType) =>");
        sb.AppendLine("        Wyrd.Ecs.Generated.SystemRegistry.Access.TryGetValue(systemType, out var access) ? access : null;");
        sb.AppendLine();
        sb.AppendLine("    private static (IReadOnlyList<Type> Before, IReadOnlyList<Type> After) EdgesOrEmpty(Type systemType) =>");
        sb.AppendLine("        Wyrd.Ecs.Generated.SystemRegistry.Edges.TryGetValue(systemType, out var declared) ? declared : (Array.Empty<Type>(), Array.Empty<Type>());");
        sb.AppendLine();
        sb.AppendLine("    private static SystemCadence CadenceOrDefault(Type systemType) =>");
        sb.AppendLine("        Wyrd.Ecs.Generated.SystemRegistry.Cadence.TryGetValue(systemType, out var cadence) ? cadence : SystemCadence.Variable;");
        sb.AppendLine("}");
        sb.AppendLine("#pragma warning restore CS1591");
        return sb.ToString();
    }

    /// <summary>
    /// Emits the `partial` class part supplying a `QuerySystem` subclass's
    /// `EcsSystem.Execute`, calling the developer-written `Update` method (its own
    /// `ref`/`in` modifiers are the source of truth for access mode). See
    /// <see cref="AppendStateExecute"/>/<see cref="AppendEntityViewExecute"/> for the two
    /// possible emission shapes.
    /// </summary>
    internal static string RenderQuerySystemGlue(QuerySystemCandidate candidate)
    {
        // OwnDataElements(), not DataElements(): this is a caller-facing parameter list
        // (Update's own declaration and the .ForEach lambda), and DataElements() is only
        // for the shared backend's internal ordering.
        var dataElements = candidate.Shape.OwnDataElements();

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

        if (candidate.HasEntityViewParameter)
            AppendEntityViewExecute(sb, candidate, dataElements);
        else
            AppendStateExecute(sb, candidate, dataElements);

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Emits `Execute` for a shape whose `Update` does not declare `EntityView`, routed
    /// through the shape's `.ForEach&lt;TState&gt;()` extension. When `World` alone is
    /// declared, widens `TState` from bare `Time` to `(Time Time, World World)`, so this
    /// still shares the same `Process`/backend codegen unchanged.
    /// </summary>
    private static void AppendStateExecute(StringBuilder sb, QuerySystemCandidate candidate, ImmutableArray<MarkerElement> dataElements)
    {
        // Calling a ref/in parameter requires the same modifier at the call site
        // (RefKind(e), not a bare ParamName(e)). The state parameter is prepended before
        // joining, not appended by string concatenation, since that would leave a
        // trailing comma when dataElements is empty (a filter-only shape).
        string dispatchExpr;
        if (candidate.HasWorldParameter)
        {
            var lambdaParams = string.Join(", ", new[] { "in (Time Time, World World) s" }.Concat(dataElements.Select(ParamDecl)));
            var updateCallArgs = string.Join(", ", new[] { "s.Time", "s.World" }.Concat(dataElements.Select(e => $"{RefKind(e)} {ParamName(e)}")));
            dispatchExpr = $"(({candidate.Shape.ExactShapeTypeName})DefineQuery(world.Query())).ForEach((time, world), ({lambdaParams}) => Update({updateCallArgs}))";
        }
        else
        {
            var lambdaParams = string.Join(", ", new[] { "in Time t" }.Concat(dataElements.Select(ParamDecl)));
            var updateCallArgs = string.Join(", ", new[] { "t" }.Concat(dataElements.Select(e => $"{RefKind(e)} {ParamName(e)}")));
            dispatchExpr = $"(({candidate.Shape.ExactShapeTypeName})DefineQuery(world.Query())).ForEach(time, ({lambdaParams}) => Update({updateCallArgs}))";
        }

        if (candidate.ResourceProperties.IsEmpty)
        {
            sb.AppendLine("    protected override void Execute(World world, Time time) =>");
            sb.AppendLine($"        {dispatchExpr};");
            return;
        }

        sb.AppendLine("    protected override void Execute(World world, Time time)");
        sb.AppendLine("    {");
        AppendResourceFetch(sb, candidate.ResourceProperties);
        sb.AppendLine($"        {dispatchExpr};");
        AppendResourceWriteback(sb, candidate.ResourceProperties);
        sb.AppendLine("    }");
    }

    private static void AppendResourceFetch(StringBuilder sb, ImmutableArray<ResourcePropertyInfo> resources)
    {
        foreach (var r in resources)
            sb.AppendLine($"        {r.PropertyName} = world.GetResource<{r.ResourceTypeName}>();");
    }

    private static void AppendResourceWriteback(StringBuilder sb, ImmutableArray<ResourcePropertyInfo> resources)
    {
        foreach (var r in resources.Where(r => r.IsWrite))
            sb.AppendLine($"        world.GetResourceRef<{r.ResourceTypeName}>() = {r.PropertyName};");
    }

    /// <summary>
    /// Emits `Execute` for a shape whose `Update` declares `EntityView`. Bypasses the
    /// shape's `.ForEach()` extension and walks `QueryChainBackend_&lt;hash&gt;` directly,
    /// since threading the per-row `Entity` through the shared `Process` loop would cost
    /// every `.ForEach()` call site, not just this one. Uses `world[entities[i]]`, never
    /// `new EntityView(...)`, since `EntityView`'s constructor is `internal` and this
    /// generated code compiles into the consumer's own assembly.
    /// </summary>
    private static void AppendEntityViewExecute(StringBuilder sb, QuerySystemCandidate candidate, ImmutableArray<MarkerElement> dataElements)
    {
        var hash = candidate.Shape.HashName();

        var updateArgs = new List<string> { "time" };
        if (candidate.HasWorldParameter) updateArgs.Add("world");
        updateArgs.Add("world[entities[i]]");
        updateArgs.AddRange(dataElements.Select(e => $"{RefKind(e)} {ParamName(e)}[i]"));

        sb.AppendLine("    protected override void Execute(World world, Time time)");
        sb.AppendLine("    {");
        AppendResourceFetch(sb, candidate.ResourceProperties);
        sb.AppendLine($"        var query = ({candidate.Shape.ExactShapeTypeName})DefineQuery(world.Query());");
        sb.AppendLine($"        foreach (var chunk in QueryChainBackend_{hash}.Cached.Resolve(world, query.Filter))");
        sb.AppendLine("        {");
        sb.AppendLine("            var entities = chunk.Entities;");
        foreach (var e in dataElements)
            sb.AppendLine($"            var {ParamName(e)} = chunk.Access<{AccessorType(e)}>();");
        sb.AppendLine("            for (var i = 0; i < chunk.Count; i++)");
        sb.AppendLine($"                Update({string.Join(", ", updateArgs)});");
        sb.AppendLine("        }");
        AppendResourceWriteback(sb, candidate.ResourceProperties);
        sb.AppendLine("    }");
    }

    /// <summary>
    /// A stable, valid-C#-identifier suffix derived from <see cref="QueryShape.ExactShapeTypeName"/>
    /// *and* <see cref="QueryShape.Markers"/>, distinct from <see cref="QueryShapeExtensions.HashName"/>,
    /// which is derived from the order-independent <see cref="QueryShapeExtensions.DedupKey"/>
    /// instead. Markers must be folded in here, not just the type name: since
    /// <c>.Without</c>/<c>.Has</c>/<c>.Any</c> don't affect <c>TShape</c>, two shapes can
    /// share one <see cref="QueryShape.ExactShapeTypeName"/> while resolving different ref/in
    /// markers for the same component (see <c>QueryChainGenerator.DeduplicateShapes</c>'s own
    /// doc comment), so hashing the type name alone would collide their generated class names.
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
        // fully-qualified name: for a generic component type (e.g. RelationLinks<Ns.Foo>),
        // the last '.' can sit inside a generic argument's namespace, which would otherwise
        // produce a garbage identifier (including the argument's own '>').
        var genericStart = name.IndexOf('<');
        var outerTypeName = genericStart >= 0 ? name[..genericStart] : name;
        var simple = outerTypeName.Contains('.') ? outerTypeName[(outerTypeName.LastIndexOf('.') + 1)..] : outerTypeName;
        return char.ToLowerInvariant(simple[0]) + simple[1..];
    }
}
