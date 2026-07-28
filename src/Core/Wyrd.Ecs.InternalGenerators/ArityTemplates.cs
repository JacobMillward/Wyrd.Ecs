using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wyrd.Ecs.InternalGenerators;

/// <summary>
/// Shared per-arity C# source templates used by <see cref="WorldQueryMembersGenerator"/>
/// for multi-component entity creation (<c>PlaceReservedEntity</c>/<c>CreateEntityOp</c>/
/// <c>CommandBuffer.CreateEntity</c>) and their supporting <c>QuerySignature</c> cache.
/// Each arity N (component count) is a mechanical, additive extension of arity N-1, so
/// these builders are parameterized by N rather than duplicated per arity. Previously
/// also emitted the fluent <c>Query&lt;T0..TN-1&gt;</c>/<c>QueryRow&lt;T0..TN-1&gt;</c>
/// family and <c>IWorld</c>/<c>World</c>'s matching <c>Query&lt;T0..TN-1&gt;()</c>
/// members; that family was removed when queries moved to the generator-backed
/// unbounded query-shape design, which has no arity cap.
/// </summary>
internal static class ArityTemplates
{
    private static IEnumerable<int> Indices(int n) => Enumerable.Range(0, n);

    internal static string TypeParams(int n) => string.Join(", ", Indices(n).Select(i => $"T{i}"));

    internal static string WhereClauses(int n, string indent) =>
        string.Join("\n", Indices(n).Select(i => $"{indent}where T{i} : struct, IComponent"));

    internal static string WhereClausesInline(int n) =>
        string.Join(" ", Indices(n).Select(i => $"where T{i} : struct, IComponent"));

    /// <summary>Same as <see cref="WhereClausesInline"/> but without the <c>IComponent</c> constraint -- matches <c>Query&lt;TShape&gt;.With&lt;TMarker&gt;()</c>'s existing single-arg constraint (<c>where TMarker : struct</c>), since the arity-2+ `With`/`Without`/`Has`/`Any` overloads are sugar for that same call, not a new, stricter contract.</summary>
    internal static string WhereClausesPlain(int n) =>
        string.Join(" ", Indices(n).Select(i => $"where T{i} : struct"));

    /// <summary>
    /// Emits <c>QuerySignature&lt;T0..TN-1&gt;</c>: an archetype signature for a given
    /// component set, resolved once per closed generic instantiation (the same pattern
    /// <c>TypeIndex&lt;T&gt;</c> uses). Originally shared with the now-removed fluent
    /// <c>Query&lt;T0..TN-1&gt;</c> family; the only remaining caller is
    /// <see cref="PlaceReservedEntityMember"/>, which still needs a cached signature
    /// lookup to find or create an entity's target archetype on multi-component
    /// creation -- unrelated to querying, but this happened to already be the
    /// signature-caching mechanism in place, so it stays rather than being duplicated.
    /// </summary>
    internal static string QuerySignature(int n)
    {
        var tp = TypeParams(n);
        var sb = new StringBuilder();

        sb.AppendLine(n == 1
            ? "/// <summary>The archetype signature for component set T0.</summary>"
            : $"/// <summary>{n}-component overload of <see cref=\"QuerySignature{{T0}}\"/>.</summary>");
        sb.AppendLine($"internal static class QuerySignature<{tp}>");
        sb.AppendLine(WhereClauses(n, "    "));
        sb.AppendLine("{");
        var withChain = string.Join("", Indices(n).Select(i => $".With(TypeIndex<T{i}>.Value)"));
        sb.AppendLine($"    internal static readonly ArchetypeSignature Value = ArchetypeSignature.Empty{withChain};");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Emits the internal <c>World</c> method <c>PlaceReservedEntity&lt;T0..TN-1&gt;(Entity, T0, ...)</c>:
    /// places a previously-reserved entity into the archetype matching
    /// <c>T0..TN-1</c> (creating it if needed) and writes the given component values.
    /// Used only by the matching generated <c>CommandBuffer.CreateEntity&lt;T0..TN-1&gt;</c>
    /// overload's queued placement — see <see cref="CommandBufferCreateEntityMember"/>.
    /// </summary>
    internal static string PlaceReservedEntityMember(int n)
    {
        var tp = TypeParams(n);
        var where = WhereClausesInline(n);
        var parameters = string.Join(", ", Indices(n).Select(i => $"T{i} component{i}"));
        var sb = new StringBuilder();

        sb.AppendLine(n == 1
            ? "    /// <summary>Places a previously-reserved entity into the matching archetype, creating it if needed, and writes the given component value.</summary>"
            : "    /// <inheritdoc cref=\"PlaceReservedEntity{T0}(Entity, T0)\"/>");
        sb.AppendLine($"    internal void PlaceReservedEntity<{tp}>(Entity entity, {parameters}) {where}");
        sb.AppendLine("    {");
        sb.AppendLine($"        var signature = QuerySignature<{tp}>.Value;");
        sb.AppendLine("        if (!_archetypes.TryGetValue(signature, out var target))");
        sb.AppendLine("            target = CreateArchetype(signature);");
        sb.AppendLine();
        sb.AppendLine("        var row = _entityTable.Place(entity, target);");
        sb.AppendLine("        NotifyEntityCreated(entity);");
        sb.AppendLine();
        foreach (var i in Indices(n))
        {
            sb.AppendLine($"        var storage{i} = target.GetOrCreateStorage<T{i}>();");
            sb.AppendLine($"        storage{i}[row] = component{i};");
            sb.AppendLine($"        if (IsTracked(TypeIndex<T{i}>.Value)) storage{i}.MarkDirty(row, _currentTick);");
            sb.AppendLine();
        }
        sb.AppendLine("    }");
        return sb.ToString();
    }

    /// <summary>
    /// Emits <c>file static class CreateEntityOp&lt;T0..TN-1&gt;</c>: a cached,
    /// non-capturing dispatcher delegate for <see cref="CommandBufferCreateEntityMember"/>'s
    /// queued placement — one static instance per closed generic combination, created
    /// once and reused across every queued call, instead of a fresh closure allocated
    /// per call. For arity 1 the payload is boxed directly as <c>T0</c>; arity 2+ boxes
    /// one value tuple holding every component, still a single allocation per queued
    /// call regardless of arity.
    /// </summary>
    internal static string CreateEntityOpClass(int n)
    {
        var tp = TypeParams(n);
        var where = WhereClausesInline(n);
        var sb = new StringBuilder();

        sb.AppendLine(n == 1
            ? "/// <summary>Cached, non-capturing dispatcher for <see cref=\"CommandBuffer.CreateEntity{T0}(T0)\"/>'s queued placement.</summary>"
            : $"/// <inheritdoc cref=\"CreateEntityOp{{T0}}\"/>");
        sb.AppendLine($"file static class CreateEntityOp<{tp}> {where}");
        sb.AppendLine("{");
        if (n == 1)
        {
            sb.AppendLine("    internal static readonly Action<World, Entity, object?, int> Apply = (w, e, v, _) => w.PlaceReservedEntity(e, (T0)v!);");
        }
        else
        {
            var items = string.Join(", ", Indices(n).Select(i => $"t.Item{i + 1}"));
            sb.AppendLine("    internal static readonly Action<World, Entity, object?, int> Apply = (w, e, v, _) =>");
            sb.AppendLine("    {");
            sb.AppendLine($"        var t = (({tp}))v!;");
            sb.AppendLine($"        w.PlaceReservedEntity(e, {items});");
            sb.AppendLine("    };");
        }
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Emits the <c>CommandBuffer</c> method <c>CreateEntity&lt;T0..TN-1&gt;(T0, ...)</c>:
    /// reserves a real entity id immediately (same as bare <c>CommandBuffer.CreateEntity()</c>)
    /// and queues its placement with the given component values already set, going
    /// directly to the matching archetype in one step instead of creating an empty
    /// entity and queuing each component add afterward. Queues via the shared
    /// <c>QueuedCommand</c> representation (see <c>CommandBuffer.cs</c>), dispatching through
    /// the matching <see cref="CreateEntityOpClass"/> instead of a per-call closure.
    /// </summary>
    internal static string CommandBufferCreateEntityMember(int n)
    {
        var tp = TypeParams(n);
        var where = WhereClausesInline(n);
        var parameters = string.Join(", ", Indices(n).Select(i => $"T{i} component{i}"));
        var value = n == 1 ? "component0" : $"({string.Join(", ", Indices(n).Select(i => $"component{i}"))})";
        var sb = new StringBuilder();

        sb.AppendLine(n == 1
            ? "    /// <summary>\n"
                + "    /// Reserves a real <see cref=\"Entity\"/> immediately, same as\n"
                + "    /// <see cref=\"CreateEntity()\"/>, and queues its placement with the given\n"
                + "    /// component already set. The returned entity is not\n"
                + "    /// <see cref=\"World.IsAlive\"/> until <see cref=\"World.ApplyCommands()\"/> runs.\n"
                + "    /// </summary>"
            : "    /// <inheritdoc cref=\"CreateEntity{T0}(T0)\"/>");
        sb.AppendLine($"    public Entity CreateEntity<{tp}>({parameters}) {where}");
        sb.AppendLine("    {");
        sb.AppendLine("        var entity = _world.ReserveEntity();");
        sb.AppendLine($"        Enqueue(new QueuedCommand(entity, CreateEntityOp<{tp}>.Apply, {value}, 0));");
        sb.AppendLine("        return entity;");
        sb.AppendLine("    }");
        return sb.ToString();
    }

    /// <summary>
    /// Emits the `Query&lt;TShape&gt;.With&lt;T0..Tn-1&gt;()` overload: pure call-site sugar
    /// for `n` chained single-arg `.With&lt;T&gt;()` calls, producing the identical
    /// nested-tuple type -- T0 innermost (first-declared), T{n-1} outermost (last-declared) --
    /// so `ChainWalker`'s declaration-order recovery needs no changes to support it.
    /// `Filter` carries through unchanged, same as the hand-written single-arg `.With&lt;T&gt;()`.
    /// </summary>
    internal static string QueryWithMember(int n)
    {
        var tp = TypeParams(n);
        var where = WhereClausesPlain(n);
        var nestedType = "TShape";
        for (var i = 0; i < n; i++) nestedType = $"(T{i}, {nestedType})";

        var sb = new StringBuilder();
        sb.AppendLine(n == 2
            ? "    /// <summary>Adds T0..Tn-1 to the shape in one call, equivalent to chaining .With&lt;T0&gt;()...With&lt;Tn-1&gt;() individually.</summary>"
            : "    /// <inheritdoc cref=\"With{T0, T1}\"/>");
        sb.AppendLine($"    public Query<{nestedType}> With<{tp}>() {where} => new(World, Filter);");
        return sb.ToString();
    }

    /// <summary>Emits the `Query&lt;TShape&gt;.Without&lt;T0..Tn-1&gt;()` overload for arity 2+ -- forwards to `Filter`, same shape (`Query&lt;TShape&gt;`, unchanged) regardless of arity, since `Without` never touches `TShape`.</summary>
    internal static string QueryWithoutMember(int n)
    {
        var tp = TypeParams(n);
        var where = WhereClausesPlain(n);
        var chain = string.Join("", Indices(n).Select(i => $".Without<T{i}>()"));
        var sb = new StringBuilder();
        sb.AppendLine(n == 2
            ? "    /// <summary>Requires the archetype to contain none of T0..Tn-1, in one call.</summary>"
            : "    /// <inheritdoc cref=\"Without{T0, T1}\"/>");
        sb.AppendLine($"    public Query<TShape> Without<{tp}>() {where} => new(World, Filter{chain});");
        return sb.ToString();
    }

    /// <summary>Emits the `Query&lt;TShape&gt;.Has&lt;T0..Tn-1&gt;()` overload for arity 2+ -- same shape as <see cref="QueryWithoutMember"/>.</summary>
    internal static string QueryHasMember(int n)
    {
        var tp = TypeParams(n);
        var where = WhereClausesPlain(n);
        var chain = string.Join("", Indices(n).Select(i => $".Has<T{i}>()"));
        var sb = new StringBuilder();
        sb.AppendLine(n == 2
            ? "    /// <summary>Requires the archetype to contain all of T0..Tn-1, without reading their data, in one call.</summary>"
            : "    /// <inheritdoc cref=\"Has{T0, T1}\"/>");
        sb.AppendLine($"    public Query<TShape> Has<{tp}>() {where} => new(World, Filter{chain});");
        return sb.ToString();
    }

    /// <summary>Emits the `Query&lt;TShape&gt;.Any&lt;T0..Tn-1&gt;()` overload for arity 3+ -- forwards to `Filter.Any&lt;T0..Tn-1&gt;()` (itself generated, see <see cref="ArchetypeFilterAnyMember"/>), same shape (`Query&lt;TShape&gt;`, unchanged) regardless of arity.</summary>
    internal static string QueryAnyMember(int n)
    {
        var tp = TypeParams(n);
        var where = WhereClausesPlain(n);
        var sb = new StringBuilder();
        sb.AppendLine("    /// <inheritdoc cref=\"Any{T0, T1}\"/>");
        sb.AppendLine($"    public Query<TShape> Any<{tp}>() {where} => new(World, Filter.Any<{tp}>());");
        return sb.ToString();
    }

    /// <summary>Emits the `ArchetypeQuery.Any&lt;T0..Tn-1&gt;()` overload for arity 3+, delegating to the matching `ArchetypeFilter.Any&lt;T0..Tn-1&gt;()`.</summary>
    internal static string ArchetypeQueryAnyMember(int n)
    {
        var tp = TypeParams(n);
        var where = WhereClausesPlain(n);
        var sb = new StringBuilder();
        sb.AppendLine("    /// <inheritdoc cref=\"Any{T0, T1}\"/>");
        sb.AppendLine($"    public ArchetypeQuery Any<{tp}>() {where} => new(_filter.Any<{tp}>());");
        return sb.ToString();
    }

    /// <summary>Emits the `ArchetypeFilter.Any&lt;T0..Tn-1&gt;()` overload for arity 3+, appending a new group built from all n type indices instead of two.</summary>
    internal static string ArchetypeFilterAnyMember(int n)
    {
        var tp = TypeParams(n);
        var where = WhereClausesPlain(n);
        var withChain = string.Join("", Indices(n).Select(i => $".With(TypeIndex<T{i}>.Value)"));
        var sb = new StringBuilder();
        sb.AppendLine("    /// <inheritdoc cref=\"Any{T0, T1}\"/>");
        sb.AppendLine($"    public ArchetypeFilter Any<{tp}>() {where} =>");
        sb.AppendLine($"        new(Required, Excluded, AnyGroups.Add(ArchetypeSignature.Empty{withChain}));");
        return sb.ToString();
    }
}
