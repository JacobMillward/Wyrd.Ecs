using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Wyrd.Ecs.Generators.Diagnostics;

namespace Wyrd.Ecs.Generators;

/// <summary>
/// Finds every `.ForEach`/`.ParallelForEach` terminal call on a fluent query chain, and
/// every `QuerySystem` subclass's `DefineQuery` override, anywhere in the consuming
/// project's source. Extracts each one's shape (<see cref="ChainWalker"/>) and emits
/// terminal methods, `QuerySystem` glue, and a `SystemRegistry` registry entry.
/// Groups chain terminals two levels deep: exact declaration-order tuple type (one
/// overload each) nested inside logical shape (one shared backend each).
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class QueryChainGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx =>
            ctx.AddSource("AddSystemExtensions.g.cs", QueryChainEmitter.RenderAddSystemExtensions()));

        var chainCandidates = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax
                    {
                        Name: IdentifierNameSyntax { Identifier.ValueText: "ForEach" or "ParallelForEach" }
                    }
                },
                transform: static (ctx, ct) => ExtractChainCandidate((InvocationExpressionSyntax)ctx.Node, ctx.SemanticModel, ct))
            .Where(static c => c.Shape is not null || c.Diagnostic is not null)
            .WithTrackingName("QueryChainShape");

        var querySystemCandidates = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax { BaseList.Types.Count: > 0 },
                transform: static (ctx, ct) => TryExtractQuerySystem((ClassDeclarationSyntax)ctx.Node, ctx.SemanticModel, ct))
            .Where(static c => c.Candidate is not null || c.Diagnostic is not null)
            .WithTrackingName("QuerySystemCandidate");

        var edgeCandidates = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: static (ctx, ct) => ExtractEdges((ClassDeclarationSyntax)ctx.Node, ctx.SemanticModel, ct))
            .Where(static e => e.SystemTypeName is not null)
            .WithTrackingName("SystemEdges");

        var constructorCandidates = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, ct) => ExtractConstructorShape((ClassDeclarationSyntax)ctx.Node, ctx.SemanticModel, ct))
            .Where(static c => c is not null)
            .WithTrackingName("ConstructorShape");

        var collectedChains = chainCandidates.Collect();
        var collectedQuerySystems = querySystemCandidates.Collect();
        var collectedEdges = edgeCandidates.Collect();
        var collectedConstructors = constructorCandidates.Collect();
        var combined = collectedChains.Combine(collectedQuerySystems).Combine(collectedEdges).Combine(collectedConstructors).Combine(context.CompilationProvider);

        context.RegisterSourceOutput(combined, static (spc, input) =>
        {
            var ((((chainResults, querySystemResults), edgeResults), constructorResults), compilation) = input;

            foreach (var result in chainResults)
                if (result.Diagnostic is not null) spc.ReportDiagnostic(result.Diagnostic);
            foreach (var result in querySystemResults)
                if (result.Diagnostic is not null) spc.ReportDiagnostic(result.Diagnostic);

            var chains = chainResults
                .Where(c => c.Shape is not null)
                .Select(c => (Shape: c.Shape!, c.SystemTypeName))
                .ToImmutableArray();
            var querySystems = querySystemResults
                .Where(s => s.Candidate is not null)
                .Select(s => s.Candidate!)
                .ToImmutableArray();

            var (byExactShape, byDedupKey) = DeduplicateShapes(spc, chains, querySystems);

            EmitBackends(spc, byDedupKey);
            EmitOverloads(spc, byExactShape);
            EmitQuerySystemGlue(spc, querySystems);

            // Unsupported is silently skipped here, not diagnosed: unlike a bare AddSystem<T>()
            // call site (which doesn't exist as a concept until the AddSystem<T>() extension
            // itself is introduced), a class declaration alone doesn't say anything about
            // whether anyone ever tries to construct it that way. Plenty of systems are
            // legitimately always hand-constructed and passed as an instance, never through a
            // parameterless/World-only factory. Diagnosing every such declaration would flag
            // valid code. AddSystemCore (see WorldBuilder/World) is the actual point that knows
            // a bare AddSystem<T>() was attempted for a type with no Construct entry, and gives
            // a clear runtime error there instead.
            var constructors = constructorResults
                .Where(c => c!.Value.Shape != ConstructorShape.Unsupported)
                .Select(c => (c!.Value.SystemTypeName, c.Value.TakesWorld, c.Value.Resources))
                .ToList();

            var bySystemType = ComputeSystemAccess(chains, querySystems);
            var writesBySystemType = bySystemType.ToDictionary(s => s.SystemTypeName, s => s.Writes);

            // Symbol-based, not syntax-based: [RequiresSnapshotBefore] is baked into
            // compiled metadata like any other attribute, so this resolves it identically
            // whether the tagged component is declared in the current compilation or a
            // referenced assembly (e.g. Transform, declared in Wyrd.Ecs core, tagged for
            // any consumer project's own Fixed-cadence writer to discover). A syntax-only
            // scan of the current compilation's own source, tried first, silently found
            // nothing for exactly this cross-assembly case.
            var snapshotTargetCache = new Dictionary<string, string?>();
            string? FindSnapshotTarget(string componentTypeName)
            {
                if (snapshotTargetCache.TryGetValue(componentTypeName, out var cached)) return cached;

                string? target = null;
                if (compilation.GetTypeByMetadataName(componentTypeName) is { } componentSymbol)
                    foreach (var attribute in componentSymbol.GetAttributes())
                    {
                        if (attribute.AttributeClass?.ToDisplayString() != "Wyrd.Ecs.RequiresSnapshotBeforeAttribute") continue;
                        if (attribute.ConstructorArguments is not [{ Value: ITypeSymbol targetType }]) continue;
                        target = targetType.ToDisplayString();
                        break;
                    }

                snapshotTargetCache[componentTypeName] = target;
                return target;
            }

            var mergedEdges = edgeResults.Select(e =>
            {
                if (!e.IsFixedTimestep || e.SystemTypeName is null) return e;
                if (!writesBySystemType.TryGetValue(e.SystemTypeName, out var writes)) return e;

                // t != e.SystemTypeName guards against a modeling mistake, not a real
                // production case: a correctly modeled snapshot system reads the tagged
                // component and writes a different one (see Transform/PreviousTransform),
                // so it never appears in its own writesBySystemType entry for the tagged
                // component. Without this guard, a system that mistakenly both carries
                // [RequiresSnapshotBefore(typeof(Self))] as a target and writes that same
                // tagged component would get a self-referential After edge, which
                // StagePlanner has no defined behavior for.
                var inferredTargets = writes
                    .Select(FindSnapshotTarget)
                    .Where(t => t is not null)
                    .Select(t => t!)
                    .Where(t => !e.After.Contains(t) && t != e.SystemTypeName);
                var after = e.After.Concat(inferredTargets).ToList();
                return after.Count == e.After.Count ? e : e with { After = after };
            }).ToImmutableArray();

            EmitSystemAccessRegistry(spc, bySystemType, mergedEdges, constructors);
        });
    }

    private enum ConstructorShape { Parameterless, WorldParameter, WithResources, Unsupported }

    // internal, not private: QueryChainEmitter (a separate class) needs to reference this
    // when it builds the Construct factory expression.
    internal readonly record struct ResourceParameter(string ResourceTypeName, bool IsWrite);

    /// <summary>One <c>EcsSystem</c>-derived class's constructor classification for <c>AddSystem&lt;T&gt;()</c>: which factory shape the generator can emit, or <see cref="ConstructorShape.Unsupported"/> if none applies (no <c>Construct</c> entry emitted for it; see the caller in <see cref="Initialize"/> for why this stays silent here rather than reporting <see cref="WyrdDiagnostics.UnconstructableSystem"/> unconditionally).</summary>
    private readonly record struct ConstructorCandidate(
        string SystemTypeName, ConstructorShape Shape, bool TakesWorld,
        ImmutableArray<ResourceParameter> Resources, Location DiagnosticLocation);

    /// <summary>
    /// Classifies <paramref name="classDecl"/>'s constructor shape for the generator's
    /// per-type <c>SystemRegistry.Construct</c> factory: no explicit constructor at all
    /// (the compiler synthesizes a public parameterless one) or an explicit public
    /// parameterless constructor are <see cref="ConstructorShape.Parameterless"/>; an
    /// optional leading <c>ctor(World)</c> parameter followed by zero or more
    /// <c>struct, IResource</c> parameters (<c>ref</c> for write access, <c>in</c> or bare
    /// for read) is <see cref="ConstructorShape.WorldParameter"/> (zero resources) or
    /// <see cref="ConstructorShape.WithResources"/>; anything else (private-only, extra
    /// non-resource parameters, more than one public constructor) is
    /// <see cref="ConstructorShape.Unsupported"/>. Only classes actually deriving from
    /// <c>Wyrd.Ecs.EcsSystem</c> are classified; everything else returns <c>null</c>,
    /// filtered out by the caller's <c>.Where</c>.
    /// </summary>
    private static ConstructorCandidate? ExtractConstructorShape(ClassDeclarationSyntax classDecl, SemanticModel semanticModel, CancellationToken ct)
    {
        if (semanticModel.GetDeclaredSymbol(classDecl, ct) is not INamedTypeSymbol classSymbol) return null;
        if (classSymbol.IsAbstract) return null;
        if (classSymbol.IsFileLocal) return null; // same reasoning as ExtractEdges: a file-local type can never be referenced from the separate generated file Construct lives in
        if (classSymbol.ContainingType is not null) return null; // nested classes not supported, same restriction and reason as QuerySystemCandidate: a private/protected nested type is inaccessible from the separate generated file Construct lives in
        if (!InheritsFromEcsSystem(classSymbol)) return null;

        var location = classDecl.Identifier.GetLocation();
        var systemTypeName = classSymbol.ToDisplayString();
        var noResources = ImmutableArray<ResourceParameter>.Empty;

        // No explicit constructor at all: the compiler synthesizes a public parameterless one.
        if (classSymbol.Constructors.Length == 0)
            return new ConstructorCandidate(systemTypeName, ConstructorShape.Parameterless, false, noResources, location);

        var publicCtors = classSymbol.Constructors
            .Where(c => c.DeclaredAccessibility == Accessibility.Public && !c.IsStatic)
            .ToList();
        if (publicCtors.Count != 1)
            return new ConstructorCandidate(systemTypeName, ConstructorShape.Unsupported, false, noResources, location);

        var parameters = publicCtors[0].Parameters;
        if (parameters.Length == 0)
            return new ConstructorCandidate(systemTypeName, ConstructorShape.Parameterless, false, noResources, location);

        var takesWorld = parameters[0].Type is INamedTypeSymbol { Name: "World" } worldType
            && worldType.ContainingNamespace.ToDisplayString() == "Wyrd.Ecs";
        var remaining = takesWorld ? parameters.RemoveAt(0) : parameters;

        // remaining.Length == 0 here only when takesWorld is true: parameters.Length == 0
        // already returned above, so !takesWorld (remaining == parameters unchanged) means
        // remaining.Length is at least 1.
        if (remaining.Length == 0)
            return new ConstructorCandidate(systemTypeName, ConstructorShape.WorldParameter, true, noResources, location);

        var resources = ImmutableArray.CreateBuilder<ResourceParameter>(remaining.Length);
        foreach (var parameter in remaining)
        {
            if (parameter.RefKind is not (RefKind.None or RefKind.In or RefKind.Ref))
                return new ConstructorCandidate(systemTypeName, ConstructorShape.Unsupported, false, noResources, location);
            if (!ImplementsIResource(parameter.Type))
                return new ConstructorCandidate(systemTypeName, ConstructorShape.Unsupported, false, noResources, location);
            resources.Add(new ResourceParameter(parameter.Type.ToDisplayString(), parameter.RefKind == RefKind.Ref));
        }

        return new ConstructorCandidate(systemTypeName, ConstructorShape.WithResources, takesWorld, resources.MoveToImmutable(), location);
    }

    /// <summary>True if <paramref name="type"/> is a struct implementing <c>Wyrd.Ecs.IResource</c>.</summary>
    private static bool ImplementsIResource(ITypeSymbol type) =>
        type.TypeKind == TypeKind.Struct
        && type.AllInterfaces.Any(i => i is { Name: "IResource", ContainingNamespace.Name: "Ecs" } && i.ContainingNamespace.ToDisplayString() == "Wyrd.Ecs");

    /// <summary>True if <paramref name="type"/> derives (directly or transitively) from <c>Wyrd.Ecs.EcsSystem</c>.</summary>
    private static bool InheritsFromEcsSystem(INamedTypeSymbol type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
            if (current is { Name: "EcsSystem", ContainingNamespace.Name: "Ecs" } && current.ContainingNamespace.ToDisplayString() == "Wyrd.Ecs")
                return true;
        return false;
    }

    /// <summary>One class declaration's discovered <c>[RunBefore]</c>/<c>[RunAfter]</c>/<c>[Phase]</c>/<c>[FixedTimestep]</c> edges/cadence, or <c>default</c> (filtered out by the <c>.Where</c> below) if it declares none.</summary>
    private readonly record struct EdgeResult(string? SystemTypeName, List<string> Before, List<string> After, bool IsFixedTimestep);

    /// <summary>
    /// Reads <c>[RunBefore(typeof(X))]</c>/<c>[RunAfter(typeof(X))]</c>/<c>[Phase(Phase.X)]</c>
    /// off a class declaration via the semantic model, at compile time. Not limited to
    /// <c>EcsSystem</c> subclasses: any class carrying the attributes is a real edge to
    /// capture, and non-<c>EcsSystem</c>/<c>MarkerSystem</c> targets are already rejected
    /// downstream at graph-resolution time.
    /// </summary>
    /// <remarks>
    /// A <c>file</c>-scoped class (or edge target) can never work here, same reason as
    /// <see cref="WyrdDiagnostics.FileLocalComponentType"/>: the emitted
    /// <c>SystemRegistry.Edges</c> entry lives in a separate generated file, which can
    /// never reference a type scoped elsewhere. Silently skipped, matching every other
    /// unrecognized-shape path in this generator.
    /// </remarks>
    private static EdgeResult ExtractEdges(ClassDeclarationSyntax classDecl, SemanticModel semanticModel, CancellationToken ct)
    {
        if (semanticModel.GetDeclaredSymbol(classDecl, ct) is not INamedTypeSymbol classSymbol) return default;
        if (classSymbol.IsFileLocal) return default;

        var before = new List<string>();
        var after = new List<string>();
        var isFixedTimestep = false;

        foreach (var attributeList in classDecl.AttributeLists)
            foreach (var attribute in attributeList.Attributes)
            {
                if (semanticModel.GetTypeInfo(attribute, ct).Type is not { } attributeType) continue;
                var attributeName = attributeType.ToDisplayString();

                if (attributeName == "Wyrd.Ecs.FixedTimestepAttribute")
                {
                    isFixedTimestep = true;
                    continue;
                }

                if (attributeName == "Wyrd.Ecs.PhaseAttribute")
                {
                    // Phase.Update (the default) is a genuine no-op: no member name match
                    // below, nothing added to before/after, identical to the attribute
                    // being absent entirely.
                    if (attribute.ArgumentList is not { Arguments: [{ Expression: var phaseExpr }] }) continue;
                    var constant = semanticModel.GetConstantValue(phaseExpr, ct);
                    if (!constant.HasValue) continue;
                    if (semanticModel.GetTypeInfo(phaseExpr, ct).Type is not { TypeKind: TypeKind.Enum } phaseType) continue;

                    var memberName = phaseType.GetMembers()
                        .OfType<IFieldSymbol>()
                        .FirstOrDefault(f => f.HasConstantValue && Equals(f.ConstantValue, constant.Value))
                        ?.Name;

                    if (memberName == "PreUpdate") before.Add("Wyrd.Ecs.StartOfUpdatePhase");
                    else if (memberName == "PostUpdate") after.Add("Wyrd.Ecs.EndOfUpdatePhase");
                    continue;
                }

                if (attributeName is not ("Wyrd.Ecs.RunBeforeAttribute" or "Wyrd.Ecs.RunAfterAttribute")) continue;
                if (attribute.ArgumentList is not { Arguments: [{ Expression: TypeOfExpressionSyntax { Type: var targetTypeSyntax } }] }) continue;
                if (semanticModel.GetTypeInfo(targetTypeSyntax, ct).Type is not INamedTypeSymbol { IsFileLocal: false } targetType) continue;

                var targetName = targetType.ToDisplayString();
                if (attributeName == "Wyrd.Ecs.RunBeforeAttribute") before.Add(targetName);
                else after.Add(targetName);
            }

        if (before.Count == 0 && after.Count == 0 && !isFixedTimestep) return default;
        return new EdgeResult(classSymbol.ToDisplayString(), before, after, isFixedTimestep);
    }

    /// <summary>One <c>.ForEach</c>/<c>.ParallelForEach</c> syntax node's extraction result: either a real <see cref="QueryShape"/>, or a <see cref="Diagnostic"/> explaining why one could not be produced (currently only <see cref="WyrdDiagnostics.FileLocalComponentType"/> reaches this path deliberately; every other unrecognized shape stays silent, since it is not this generator's job to explain every possible reason a chain does not resolve).</summary>
    private readonly record struct ChainCandidateResult(QueryShape? Shape, string? SystemTypeName, Diagnostic? Diagnostic);

    private static ChainCandidateResult ExtractChainCandidate(InvocationExpressionSyntax invocation, SemanticModel semanticModel, CancellationToken ct)
    {
        // Checked before ChainWalker.TryExtractShape: a file-local component type must
        // never reach the exact-shape/dedup pipeline, since that's what lets it silently
        // collide with an unrelated, ordinarily-scoped type of the same simple name
        // elsewhere in the compilation (see WYRD004's own doc comment).
        if (invocation.Expression is MemberAccessExpressionSyntax { Expression: var receiverExpr }
            && semanticModel.GetTypeInfo(receiverExpr, ct).Type is INamedTypeSymbol receiverType
            && ChainWalker.TryFindFileLocalComponentType(receiverType, ct) is { } fileLocalTypeName)
        {
            return new ChainCandidateResult(null, null, Diagnostic.Create(WyrdDiagnostics.FileLocalComponentType, invocation.GetLocation(), fileLocalTypeName));
        }

        var shape = ChainWalker.TryExtractShape(invocation, semanticModel, ct);
        var systemTypeName = shape is null ? null : ChainWalker.TryFindEnclosingSystemType(invocation, semanticModel, ct);
        return new ChainCandidateResult(shape, systemTypeName, null);
    }

    /// <summary>
    /// Two-level grouping: chains/querySystems collapse to one shape per distinct
    /// <see cref="QueryShape.ExactShapeTypeName"/> (one overload each), nested inside one
    /// per distinct <see cref="QueryShapeExtensions.DedupKey"/> (one shared backend each).
    /// Since <c>.Without</c>/<c>.Has</c>/<c>.Any</c> don't affect <c>TShape</c>, two
    /// otherwise-unrelated queries can share the exact same closed <c>Query&lt;TShape&gt;</c>
    /// type while resolving different ref/in markers for it. Emitting both as separate
    /// overloads compiles but produces a hard-to-diagnose CS0121 "ambiguous call" at every
    /// consumer call site, since C# reports both as equally applicable rather than
    /// rejecting the wrong one. So conflicting groups are reported via
    /// <see cref="WyrdDiagnostics.ConflictingAccessForSameShape"/> instead, keeping only
    /// the first shape encountered per <see cref="QueryShape.ExactShapeTypeName"/>.
    /// </summary>
    private static (List<QueryShape> ByExactShape, List<QueryShape> ByDedupKey) DeduplicateShapes(
        SourceProductionContext spc,
        ImmutableArray<(QueryShape Shape, string? SystemTypeName)> chains,
        ImmutableArray<QuerySystemCandidate> querySystems)
    {
        var allShapes = chains.Select(c => c.Shape).Concat(querySystems.Select(s => s.Shape));

        var byExactShape = new List<QueryShape>();
        foreach (var group in allShapes.GroupBy(s => s.ExactShapeTypeName))
        {
            var distinctShapes = group.Distinct().ToList();
            if (distinctShapes.Count > 1)
                spc.ReportDiagnostic(Diagnostic.Create(WyrdDiagnostics.ConflictingAccessForSameShape, Location.None, group.Key));

            byExactShape.Add(distinctShapes[0]);
        }

        var byDedupKey = byExactShape
            .GroupBy(s => s.DedupKey())
            .Select(g => g.First())
            .ToList();

        return (byExactShape, byDedupKey);
    }

    private static void EmitBackends(SourceProductionContext spc, IEnumerable<QueryShape> byDedupKey)
    {
        foreach (var shape in byDedupKey)
            spc.AddSource($"QueryChainBackend.{shape.HashName()}.g.cs", QueryChainEmitter.RenderBackend(shape));
    }

    private static void EmitOverloads(SourceProductionContext spc, IEnumerable<QueryShape> byExactShape)
    {
        foreach (var shape in byExactShape)
        {
            spc.AddSource($"QueryChainForEach.{QueryChainEmitter.ExactShapeHash(shape)}.g.cs", QueryChainEmitter.RenderForEachOverload(shape));
            spc.AddSource($"QueryChainPredicateForEach.{QueryChainEmitter.ExactShapeHash(shape)}.g.cs", QueryChainEmitter.RenderPredicateForEachOverload(shape));
            spc.AddSource($"QueryChainParallelForEach.{QueryChainEmitter.ExactShapeHash(shape)}.g.cs", QueryChainEmitter.RenderParallelForEachOverload(shape));
        }
    }

    private static void EmitQuerySystemGlue(SourceProductionContext spc, ImmutableArray<QuerySystemCandidate> querySystems)
    {
        foreach (var system in querySystems)
            spc.AddSource($"QuerySystem.{system.Namespace}.{system.ClassName}.g.cs", QueryChainEmitter.RenderQuerySystemGlue(system));
    }

    /// <summary>
    /// Every declared system's Reads/Writes footprint: query-chain/<c>QuerySystem</c> data
    /// elements plus <c>[Resource]</c> property access, merged per system type. Computed
    /// once and shared by <see cref="Initialize"/> (which only needs the Writes side, to
    /// match a system's access against <c>[RequiresSnapshotBefore]</c>-tagged components)
    /// and <see cref="EmitSystemAccessRegistry"/> (which needs the full Reads/Writes pair
    /// for the emitted registry), rather than each recomputing its own copy.
    /// </summary>
    private static List<(string SystemTypeName, List<string> Reads, List<string> Writes)> ComputeSystemAccess(
        ImmutableArray<(QueryShape Shape, string? SystemTypeName)> chains,
        ImmutableArray<QuerySystemCandidate> querySystems)
    {
        var accessFromChains = chains
            .Where(c => c.SystemTypeName is not null)
            .Select(c => (SystemTypeName: c.SystemTypeName!, c.Shape));
        var accessFromQuerySystems = querySystems
            .Select(s => (SystemTypeName: s.Namespace.Length > 0 ? $"{s.Namespace}.{s.ClassName}" : s.ClassName, s.Shape));

        // Every QuerySystemCandidate already contributes exactly one entry above, even one
        // whose shape has zero data elements (an empty query with only [Resource]
        // properties), so every QuerySystem lands in the GroupBy below with at least an
        // empty Reads/Writes pair - resource access only needs a lookup merged into the
        // existing Select, not a second pass for systems the grouping might otherwise miss.
        var resourceAccessFromQuerySystems = querySystems
            .SelectMany(s => s.ResourceProperties.Select(r => (
                SystemTypeName: s.Namespace.Length > 0 ? $"{s.Namespace}.{s.ClassName}" : s.ClassName,
                r.ResourceTypeName,
                r.IsWrite)))
            .ToLookup(r => r.SystemTypeName);

        return accessFromChains.Concat(accessFromQuerySystems)
            .GroupBy(c => c.SystemTypeName)
            .Select(g =>
            {
                var resourceEntries = resourceAccessFromQuerySystems[g.Key];
                var reads = g.SelectMany(c => c.Shape.DataElements().Where(m => m.Kind == MarkerKind.Reads).Select(m => m.ComponentTypeName))
                    .Concat(resourceEntries.Where(r => !r.IsWrite).Select(r => r.ResourceTypeName))
                    .Distinct().OrderBy(n => n, System.StringComparer.Ordinal).ToList();
                var writes = g.SelectMany(c => c.Shape.DataElements().Where(m => m.Kind == MarkerKind.Writes).Select(m => m.ComponentTypeName))
                    .Concat(resourceEntries.Where(r => r.IsWrite).Select(r => r.ResourceTypeName))
                    .Distinct().OrderBy(n => n, System.StringComparer.Ordinal).ToList();
                return (SystemTypeName: g.Key, Reads: reads, Writes: writes);
            })
            .ToList();
    }

    private static void EmitSystemAccessRegistry(
        SourceProductionContext spc,
        List<(string SystemTypeName, List<string> Reads, List<string> Writes)> bySystemType,
        ImmutableArray<EdgeResult> edges,
        IReadOnlyList<(string SystemTypeName, bool TakesWorld, ImmutableArray<ResourceParameter> Resources)> constructors)
    {
        var byEdgeSystemType = edges
            .Select(e => (SystemTypeName: e.SystemTypeName!, e.Before, e.After))
            .ToList();

        var fixedTimestepSystemTypeNames = edges
            .Where(e => e.IsFixedTimestep)
            .Select(e => e.SystemTypeName!)
            .ToList();

        spc.AddSource("SystemRegistry.g.cs", QueryChainEmitter.RenderSystemAccessRegistry(bySystemType, byEdgeSystemType, fixedTimestepSystemTypeNames, constructors));
    }

    /// <summary>One <c>QuerySystem</c> class syntax node's extraction result: either a real <see cref="QuerySystemCandidate"/>, or a <see cref="Diagnostic"/> explaining why one could not be produced. Same rationale as <see cref="ChainCandidateResult"/>: only the file-local check reaches this path deliberately; every other "not a valid QuerySystem" reason stays silent.</summary>
    private readonly record struct QuerySystemResult(QuerySystemCandidate? Candidate, Diagnostic? Diagnostic);

    private static QuerySystemResult TryExtractQuerySystem(ClassDeclarationSyntax classDecl, SemanticModel semanticModel, CancellationToken ct)
    {
        if (semanticModel.GetDeclaredSymbol(classDecl, ct) is not INamedTypeSymbol classSymbol) return default;
        if (classSymbol.ContainingType is not null) return default; // nested classes not supported
        if (classSymbol.BaseType is not { Name: "QuerySystem" } baseType) return default;
        if (baseType.ContainingNamespace?.ToDisplayString() != "Wyrd.Ecs") return default;

        // Find the real override of QuerySystem.DefineQuery: a genuine abstract member,
        // so this is a symbol comparison against the one specific member, not a
        // name/static/parameter-shape guess that could false-positive on an unrelated
        // method.
        var defineQueryOnBase = baseType.GetMembers("DefineQuery").OfType<IMethodSymbol>().FirstOrDefault();
        if (defineQueryOnBase is null) return default;

        var defineQueryMethod = classSymbol.GetMembers("DefineQuery").OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.IsOverride && SymbolEqualityComparer.Default.Equals(m.OverriddenMethod?.OriginalDefinition, defineQueryOnBase));
        if (defineQueryMethod is null) return default;

        // Read the shape from DefineQuery's return *expression*, not its declared return
        // type: lets DefineQuery declare the non-generic IQuery instead of restating
        // the exact tuple shape. Only a single-expression body (arrow or
        // block-with-one-return) is recognized; anything else falls through to the
        // ordinary "does not implement abstract member Execute" compiler error the same
        // as a missing DefineQuery would.
        if (defineQueryMethod.DeclaringSyntaxReferences is not [var defineQuerySyntaxRef, ..]) return default;
        if (defineQuerySyntaxRef.GetSyntax(ct) is not MethodDeclarationSyntax { ExpressionBody.Expression: var returnExpr }) return default;

        var defineQuerySemanticModel = semanticModel.Compilation.GetSemanticModel(defineQuerySyntaxRef.SyntaxTree);
        if (defineQuerySemanticModel.GetTypeInfo(returnExpr, ct).Type is not INamedTypeSymbol returnType) return default;

        // Same reasoning and same diagnostic as ExtractChainCandidate: checked before shape
        // extraction, so a file-local component type never reaches the exact-shape/dedup
        // pipeline at all.
        if (ChainWalker.TryFindFileLocalComponentType(returnType, ct) is { } fileLocalTypeName)
            return new QuerySystemResult(null, Diagnostic.Create(WyrdDiagnostics.FileLocalComponentType, returnExpr.GetLocation(), fileLocalTypeName));

        var shape = ChainWalker.TryExtractShapeFromQueryType(returnType, ct);
        if (shape is null) return default;

        var namespaceName = classSymbol.ContainingNamespace is { IsGlobalNamespace: false } ns ? ns.ToDisplayString() : "";
        var resourceProperties = ExtractResourceProperties(classSymbol);

        // Update is name-convention-recognized, not a real override: its parameter list
        // depends on unpacking an arbitrary TShape tuple, which isn't expressible as a
        // fixed C# signature (see QuerySystem.cs's doc comment). A missing/malformed
        // Update falls through to WYRD002, not this method returning nothing for a reason a
        // developer can't see, so this only bails out here for "no method named Update
        // exists at all" (the true "nothing to resolve against" case, for both the
        // filter-only and data-bearing shapes below); count/type/order mismatches against
        // an Update that *does* exist are WYRD002's job, checked separately by its
        // analyzer, not blocked here.
        var updateMethod = classSymbol.GetMembers("Update").OfType<IMethodSymbol>().FirstOrDefault(m => !m.IsStatic);
        if (updateMethod is null) return default;

        var classification = QuerySystemUpdateShape.Classify(updateMethod.Parameters);
        if (!classification.IsValid) return default;

        if (shape.PendingDataElements.IsEmpty)
        {
            if (updateMethod.Parameters.Length != classification.ComponentStartIndex) return default; // no data parameters beyond Time/World/EntityView

            return new QuerySystemResult(new QuerySystemCandidate
            {
                Namespace = namespaceName,
                ClassName = classSymbol.Name,
                Shape = shape,
                HasWorldParameter = classification.HasWorld,
                HasEntityViewParameter = classification.HasEntityView,
                ResourceProperties = resourceProperties,
            }, null);
        }

        var dataParameters = updateMethod.Parameters.Skip(classification.ComponentStartIndex).ToImmutableArray();
        if (dataParameters.Length != shape.PendingDataElements.Length) return default;

        var refKinds = dataParameters.Select(p => p.RefKind).ToImmutableArray();
        var resolvedShape = ChainWalker.ResolveAccessKinds(shape, refKinds);
        if (resolvedShape is null) return default;

        return new QuerySystemResult(new QuerySystemCandidate
        {
            Namespace = namespaceName,
            ClassName = classSymbol.Name,
            Shape = resolvedShape,
            HasWorldParameter = classification.HasWorld,
            HasEntityViewParameter = classification.HasEntityView,
            ResourceProperties = resourceProperties,
        }, null);
    }

    /// <summary>Collects every `[Resource]`-tagged property on <paramref name="classSymbol"/>, its read/write mode taken from whether its setter is public.</summary>
    private static ImmutableArray<ResourcePropertyInfo> ExtractResourceProperties(INamedTypeSymbol classSymbol)
    {
        var result = ImmutableArray.CreateBuilder<ResourcePropertyInfo>();
        foreach (var property in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            var hasResourceAttribute = property.GetAttributes()
                .Any(a => a.AttributeClass is { Name: "ResourceAttribute", ContainingNamespace.Name: "Ecs" } ac && ac.ContainingNamespace.ToDisplayString() == "Wyrd.Ecs");
            if (!hasResourceAttribute) continue;

            var isWrite = property.SetMethod is { DeclaredAccessibility: Accessibility.Public };
            result.Add(new ResourcePropertyInfo(property.Name, property.Type.ToDisplayString(), isWrite));
        }
        return result.ToImmutable();
    }
}
