using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
        context.RegisterPostInitializationOutput(ctx =>
            ctx.AddSource("InterceptsLocationAttribute.g.cs", QueryChainEmitter.RenderInterceptsLocationAttributeSource()));

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
                .Select(c => (Shape: c.Shape!, c.SystemTypeName, c.InterceptableLocation, c.Uniform, c.TerminalKind, c.CallSiteLocation))
                .ToImmutableArray();
            var querySystems = querySystemResults
                .Where(s => s.Candidate is not null)
                .Select(s => s.Candidate!)
                .ToImmutableArray();

            var (allVariants, canonical, byDedupKey) = DeduplicateShapes(chains, querySystems);

            EmitBackends(spc, byDedupKey);
            EmitOverloads(spc, canonical);
            EmitInterceptorsAndTargets(spc, canonical, chains);
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

    /// <summary>Which overload kind a `.ForEach`/`.ParallelForEach` call site resolves to -- determines which interceptor emission path (if any) applies. Only <see cref="Action"/> call sites are eligible for interception today; see <see cref="QueryChainGenerator.EmitInterceptorsAndTargets"/>.</summary>
    private enum ChainTerminalKind { Action, Predicate, Parallel }

    /// <summary>One <c>.ForEach</c>/<c>.ParallelForEach</c> syntax node's extraction result: either a real <see cref="QueryShape"/>, or a <see cref="Diagnostic"/> explaining why one could not be produced (currently only <see cref="WyrdDiagnostics.FileLocalComponentType"/> reaches this path deliberately; every other unrecognized shape stays silent, since it is not this generator's job to explain every possible reason a chain does not resolve).</summary>
    private readonly record struct ChainCandidateResult(
        QueryShape? Shape, string? SystemTypeName, InterceptableLocation? InterceptableLocation,
        bool Uniform, ChainTerminalKind TerminalKind, Location CallSiteLocation, Diagnostic? Diagnostic);

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
            return new ChainCandidateResult(null, null, null, false, ChainTerminalKind.Action, Location.None, Diagnostic.Create(WyrdDiagnostics.FileLocalComponentType, invocation.GetLocation(), fileLocalTypeName));
        }

        var shape = ChainWalker.TryExtractShape(invocation, semanticModel, ct);
        var systemTypeName = shape is null ? null : ChainWalker.TryFindEnclosingSystemType(invocation, semanticModel, ct);

        if (shape is null) return new ChainCandidateResult(null, systemTypeName, null, false, ChainTerminalKind.Action, Location.None, null);

#pragma warning disable RSEXPERIMENTAL002
        var interceptableLocation = semanticModel.GetInterceptableLocation(invocation, ct);
#pragma warning restore RSEXPERIMENTAL002
        var uniform = invocation.ArgumentList.Arguments.Count > 1;
        var terminalKind = ClassifyTerminalKind(invocation, semanticModel, ct);

        return new ChainCandidateResult(shape, systemTypeName, interceptableLocation, uniform, terminalKind, invocation.GetLocation(), null);
    }

    /// <summary>
    /// Classifies which overload a call site will resolve to. `ParallelForEach` is always
    /// <see cref="ChainTerminalKind.Parallel"/>. A `ForEach` call is
    /// <see cref="ChainTerminalKind.Predicate"/> if its lambda's body provably returns
    /// `bool` -- an expression body whose own type (checked via the semantic model; sound,
    /// since the expression's type doesn't depend on which overload the invocation itself
    /// resolves to -- that resolution doesn't exist yet at this point in the pipeline) is
    /// `bool`, or a block body containing any `return &lt;boolExpr&gt;;`. A real action
    /// call site's expression body is routinely non-void (e.g. `p.X += v.X * 0f`, a valid
    /// statement expression whose own type is discarded by the void delegate) -- checking
    /// specifically for `bool`, not merely "has an expression body", is what keeps this
    /// from misclassifying that shape as Predicate. Anything else is
    /// <see cref="ChainTerminalKind.Action"/>. Only <see cref="ChainTerminalKind.Action"/>
    /// call sites are eligible for interception (see <c>EmitInterceptorsAndTargets</c>).
    /// </summary>
    private static ChainTerminalKind ClassifyTerminalKind(InvocationExpressionSyntax invocation, SemanticModel semanticModel, CancellationToken ct)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name: IdentifierNameSyntax { Identifier.ValueText: var methodName } }) return ChainTerminalKind.Action;
        if (methodName == "ParallelForEach") return ChainTerminalKind.Parallel;
        if (invocation.ArgumentList.Arguments is not [.., { Expression: ParenthesizedLambdaExpressionSyntax lambda }]) return ChainTerminalKind.Action;

        bool IsBool(ExpressionSyntax e) => semanticModel.GetTypeInfo(e, ct).Type?.SpecialType == SpecialType.System_Boolean;

        if (lambda.ExpressionBody is { } exprBody) return IsBool(exprBody) ? ChainTerminalKind.Predicate : ChainTerminalKind.Action;
        if (lambda.Block is { } block && block.DescendantNodes().OfType<ReturnStatementSyntax>().Any(r => r.Expression is not null && IsBool(r.Expression))) return ChainTerminalKind.Predicate;

        return ChainTerminalKind.Action;
    }

    /// <summary>
    /// Groups every discovered shape by <see cref="QueryShape.ExactShapeTypeName"/>.
    /// Since <c>.Without</c>/<c>.Has</c>/<c>.Any</c> don't affect <c>TShape</c>, two
    /// otherwise-unrelated queries can share the exact same closed <c>Query&lt;TShape&gt;</c>
    /// type while resolving different ref/in markers for it. Rather than erroring on that
    /// (the old WYRD003 behavior), every distinct variant is kept (<c>AllVariants</c>, for
    /// interceptor targeting).
    ///
    /// <c>Canonical</c> is the single public overload every call site for that exact type
    /// name binds to. **When only one distinct variant exists for a type name (the common,
    /// non-colliding case), that variant *is* canonical, unchanged** -- today's exact
    /// behavior, zero precision loss, no interceptor needed, regardless of whether it
    /// happens to be all-Writes, all-Reads, or mixed. Only when genuinely multiple distinct
    /// variants share one type name is canonical synthesized as an all-<c>Writes</c>
    /// shape instead -- the one form every real variant's lambda can legally convert to
    /// (an `in`-lambda converts to a `ref` delegate; not vice versa), so it alone can be
    /// unambiguous for every colliding call site. This is what "needs interception" means
    /// downstream: a call site whose own shape doesn't equal its group's canonical shape.
    ///
    /// <c>ByDedupKey</c> feeds <see cref="EmitBackends"/> and covers every real variant plus
    /// each synthesized canonical, so the pessimistic all-`Mut` backend a non-intercepted
    /// synthesized-canonical call would use is always available.
    /// </summary>
    private static (List<QueryShape> AllVariants, List<QueryShape> Canonical, List<QueryShape> ByDedupKey) DeduplicateShapes(
        ImmutableArray<(QueryShape Shape, string? SystemTypeName, InterceptableLocation? InterceptableLocation, bool Uniform, ChainTerminalKind TerminalKind, Location CallSiteLocation)> chains,
        ImmutableArray<QuerySystemCandidate> querySystems)
    {
        var allShapes = chains.Select(c => c.Shape).Concat(querySystems.Select(s => s.Shape));

        var allVariants = new List<QueryShape>();
        var canonical = new List<QueryShape>();
        foreach (var group in allShapes.GroupBy(s => s.ExactShapeTypeName))
        {
            var distinctShapes = group.Distinct().ToList();
            allVariants.AddRange(distinctShapes);
            canonical.Add(distinctShapes.Count == 1 ? distinctShapes[0] : AllWrites(distinctShapes[0]));
        }

        var byDedupKey = allVariants.Concat(canonical)
            .GroupBy(s => s.DedupKey())
            .Select(g => g.First())
            .ToList();

        return (allVariants, canonical, byDedupKey);
    }

    /// <summary>The all-<see cref="MarkerKind.Writes"/> variant of <paramref name="shape"/>: same exact type and component order, every marker forced to Writes. This is the shape every public `.ForEach` overload's delegate is generated from.</summary>
    private static QueryShape AllWrites(QueryShape shape) => new()
    {
        ExactShapeTypeName = shape.ExactShapeTypeName,
        Markers = shape.Markers.Select(m => m with { Kind = MarkerKind.Writes }).ToImmutableArray(),
        PendingDataElements = shape.PendingDataElements,
    };

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

    /// <summary>
    /// For every chain call site whose own shape doesn't structurally equal its group's
    /// canonical shape (see <see cref="DeduplicateShapes"/> -- only possible when a genuine
    /// collision synthesized an all-Writes canonical distinct from this real variant),
    /// emits a shared per-variant target method (once per distinct variant) and a
    /// `[InterceptsLocation]` method for that exact call site forwarding to it. A
    /// non-colliding shape's only variant always *is* its own canonical, so this is a
    /// no-op for the common case -- no interceptor, no precision loss, unchanged from
    /// today. `ChainTerminalKind.Action` is the only kind this covers; a colliding
    /// Predicate or Parallel call site instead gets
    /// <see cref="WyrdDiagnostics.UnsupportedAccessVariantInterception"/>.
    /// </summary>
    private static void EmitInterceptorsAndTargets(
        SourceProductionContext spc,
        List<QueryShape> canonical,
        ImmutableArray<(QueryShape Shape, string? SystemTypeName, InterceptableLocation? InterceptableLocation, bool Uniform, ChainTerminalKind TerminalKind, Location CallSiteLocation)> chains)
    {
        var canonicalByExactShapeTypeName = canonical.ToDictionary(s => s.ExactShapeTypeName);
        var emittedTargets = new HashSet<string>();
        var interceptorIndex = 0;

        foreach (var (shape, _, location, uniform, terminalKind, callSiteLocation) in chains)
        {
            var canonicalShape = canonicalByExactShapeTypeName[shape.ExactShapeTypeName];
            if (shape.Equals(canonicalShape)) continue; // this call site's own variant already is canonical: no collision, nothing to intercept

            if (terminalKind != ChainTerminalKind.Action)
            {
                var readsMarkers = shape.Markers.Where(m => m.Kind == MarkerKind.Reads).ToList();
                var componentNames = string.Join(", ", readsMarkers.Select(m => m.ComponentTypeName));
                spc.ReportDiagnostic(Diagnostic.Create(WyrdDiagnostics.UnsupportedAccessVariantInterception, callSiteLocation, componentNames));
                continue;
            }

            if (location is null) continue; // no interceptable location resolved -- nothing to attach to

            var variantHash = QueryChainEmitter.ExactShapeHash(shape);
            if (emittedTargets.Add(variantHash))
                spc.AddSource($"QueryChainInterceptorTarget.{variantHash}.g.cs", QueryChainEmitter.RenderInterceptorTarget(canonicalShape, shape));

#pragma warning disable RSEXPERIMENTAL002
            var attributeSyntax = location.GetInterceptsLocationAttributeSyntax();
#pragma warning restore RSEXPERIMENTAL002
            var uniqueSuffix = $"{variantHash}_{interceptorIndex++}";
            spc.AddSource($"QueryChainInterceptor.{uniqueSuffix}.g.cs",
                QueryChainEmitter.RenderInterceptor(canonicalShape, shape, attributeSyntax, uniform, uniqueSuffix));
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
        ImmutableArray<(QueryShape Shape, string? SystemTypeName, InterceptableLocation? InterceptableLocation, bool Uniform, ChainTerminalKind TerminalKind, Location CallSiteLocation)> chains,
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
