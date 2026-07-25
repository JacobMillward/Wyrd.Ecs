using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wyrd.Ecs.Generators;

/// <summary>
/// Shared per-arity C# source templates used by both <see cref="QueryTypesGenerator"/>
/// and <see cref="WorldQueryMembersGenerator"/>. Each arity N (component count) is a
/// mechanical, additive extension of arity N-1 — see the design's Unified entity-tier
/// query section ("not N parallel implementations") — so these builders are
/// parameterized by N rather than duplicated per arity. Mirrors, field-for-field and
/// branch-for-branch, the hand-authored arity 1-3 shapes this generator replaces.
/// </summary>
internal static class ArityTemplates
{
    private static IEnumerable<int> Indices(int n) => Enumerable.Range(0, n);

    internal static string TypeParams(int n) => string.Join(", ", Indices(n).Select(i => $"T{i}"));

    internal static string WhereClauses(int n, string indent) =>
        string.Join("\n", Indices(n).Select(i => $"{indent}where T{i} : struct, IComponent"));

    internal static string WhereClausesInline(int n) =>
        string.Join(" ", Indices(n).Select(i => $"where T{i} : struct, IComponent"));

    /// <summary>Emits <c>QueryRow&lt;T0..TN-1&gt;</c>, the per-row accessor for arity <paramref name="n"/>.</summary>
    internal static string QueryRow(int n)
    {
        var tp = TypeParams(n);
        var sb = new StringBuilder();

        sb.AppendLine(n == 1
            ? "/// <summary>"
            : $"/// <summary>{n}-component overload of <see cref=\"QueryRow{{T0}}\"/>.</summary>");
        if (n == 1)
        {
            sb.AppendLine("/// One matched entity's row from a <see cref=\"Query{T0}\"/> (or a higher-arity");
            sb.AppendLine("/// overload — this shape is reused unchanged at every arity, see the design's");
            sb.AppendLine("/// Unified entity-tier query section). <see cref=\"Get{T}\"/> is the single accessor");
            sb.AppendLine("/// for every declared component type: it marks the entity dirty (the same");
            sb.AppendLine("/// \"access, not proven write\" semantics <see cref=\"Mut{T}\"/> already has) then");
            sb.AppendLine("/// returns a mutable reference into the pre-cached, per-archetype-transition span —");
            sb.AppendLine("/// never a fresh per-call storage lookup. See the design's Performance section.");
            sb.AppendLine("/// </summary>");
        }

        sb.AppendLine($"public readonly ref struct QueryRow<{tp}>");
        sb.AppendLine(WhereClauses(n, "    "));
        sb.AppendLine("{");

        foreach (var i in Indices(n))
        {
            sb.AppendLine($"    private readonly Span<T{i}> _items{i};");
            sb.AppendLine($"    private readonly Span<int> _lastMarkedTick{i};");
            sb.AppendLine($"    private readonly bool _tracked{i};");
        }
        sb.AppendLine("    private readonly int _tick;");
        sb.AppendLine("    private readonly int _row;");
        sb.AppendLine("    private readonly Entity _entity;");
        sb.AppendLine();

        sb.AppendLine("    internal QueryRow(");
        sb.AppendLine(string.Join(",\n", Indices(n).Select(i =>
            $"        Span<T{i}> items{i}, Span<int> lastMarkedTick{i}, bool tracked{i}")));
        sb.AppendLine("        , int tick, int row, Entity entity)");
        sb.AppendLine("    {");
        foreach (var i in Indices(n))
            sb.AppendLine($"        _items{i} = items{i}; _lastMarkedTick{i} = lastMarkedTick{i}; _tracked{i} = tracked{i};");
        sb.AppendLine("        _tick = tick;");
        sb.AppendLine("        _row = row;");
        sb.AppendLine("        _entity = entity;");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine(n == 1
            ? "    /// <summary>The entity occupying this row — free, already known by the enumerator.</summary>"
            : "    /// <inheritdoc cref=\"QueryRow{T0}.Entity\"/>");
        sb.AppendLine("    public Entity Entity => _entity;");
        sb.AppendLine();

        if (n == 1)
        {
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Marks the entity dirty for <typeparamref name=\"T\"/> (deduplicated per tick),");
            sb.AppendLine("    /// then returns a mutable reference to its <typeparamref name=\"T\"/> component.");
            sb.AppendLine("    /// <typeparamref name=\"T\"/> must be one of this row's declared type arguments —");
            sb.AppendLine("    /// C# has no generic constraint expressing \"one of these specific types\", so a");
            sb.AppendLine("    /// mismatched <typeparamref name=\"T\"/> throws at runtime instead.");
            sb.AppendLine("    /// </summary>");
        }
        else
        {
            sb.AppendLine("    /// <inheritdoc cref=\"QueryRow{T0}.Get{T}\"/>");
        }
        sb.AppendLine("    public ref T Get<T>() where T : struct, IComponent");
        sb.AppendLine("    {");
        foreach (var i in Indices(n))
        {
            sb.AppendLine($"        if (typeof(T) == typeof(T{i}))");
            sb.AppendLine("        {");
            sb.AppendLine($"            if (_tracked{i}) _lastMarkedTick{i}[_row] = _tick;");
            sb.AppendLine($"            return ref Unsafe.As<T{i}, T>(ref _items{i}[_row]);");
            sb.AppendLine("        }");
        }
        sb.AppendLine();
        var typeList = string.Join(", ", Indices(n).Select(i => $"{{typeof(T{i})}}"));
        sb.AppendLine($"        throw new InvalidOperationException($\"Get<{{typeof(T)}}>() was called on a QueryRow<{typeList}> — {{typeof(T)}} is not one of its declared component types.\");");
        sb.AppendLine("    }");

        sb.AppendLine();
        sb.AppendLine(n == 1
            ? "    /// <summary>\n"
                + "    /// Returns a mutable reference to <typeparamref name=\"T\"/> without marking\n"
                + "    /// anything dirty. Not meant to be called directly: use <see cref=\"Get{T}\"/>.\n"
                + "    /// The interceptor generator is the only intended caller, substituting this in\n"
                + "    /// for <see cref=\"Get{T}\"/> at call sites it can prove never write.\n"
                + "    /// </summary>"
            : "    /// <inheritdoc cref=\"QueryRow{T0}.GetUnmarked{T}\"/>");
        sb.AppendLine("    public ref T GetUnmarked<T>() where T : struct, IComponent");
        sb.AppendLine("    {");
        foreach (var i in Indices(n))
            sb.AppendLine($"        if (typeof(T) == typeof(T{i})) return ref Unsafe.As<T{i}, T>(ref _items{i}[_row]);");
        sb.AppendLine();
        sb.AppendLine($"        throw new InvalidOperationException($\"GetUnmarked<{{typeof(T)}}>() was called on a QueryRow<{typeList}> — {{typeof(T)}} is not one of its declared component types.\");");
        sb.AppendLine("    }");

        if (n >= 2)
        {
            sb.AppendLine();
            sb.AppendLine(n == 2
                ? "    /// <summary>\n"
                    + "    /// Read-only destructuring: copies both components out by value. Never marks\n"
                    + "    /// anything dirty — a destructured local is always a copy, never writable back\n"
                    + "    /// into storage. Writing to a component bound this way is a native C# compile\n"
                    + "    /// error (<c>CS1654</c>/<c>CS1656</c> — deconstructed <c>foreach</c> locals are\n"
                    + "    /// foreach iteration variables, which C# already refuses to write through), not\n"
                    + "    /// something this library's own analyzers need to enforce. See the design's\n"
                    + "    /// Mutation and read ergonomics section.\n"
                    + "    /// </summary>"
                : "    /// <inheritdoc cref=\"QueryRow{T0, T1}.Deconstruct\"/>");
            var outParams = string.Join(", ", Indices(n).Select(i => $"out T{i} component{i}"));
            sb.AppendLine($"    public void Deconstruct({outParams})");
            sb.AppendLine("    {");
            foreach (var i in Indices(n))
                sb.AppendLine($"        component{i} = _items{i}[_row];");
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>Emits <c>Query&lt;T0..TN-1&gt;</c>, the enumerable sequence for arity <paramref name="n"/>.</summary>
    internal static string Query(int n)
    {
        var tp = TypeParams(n);
        var sb = new StringBuilder();

        if (n == 1)
        {
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// Unified entity-tier query, replacing <c>QueryMut&lt;T&gt;</c>/<c>QueryRef&lt;T&gt;</c>");
            sb.AppendLine("/// outright: a <c>foreach</c>-able sequence of <see cref=\"QueryRow{T0}\"/>, one per");
            sb.AppendLine("/// matching entity, walking archetypes internally so no chunk or archetype");
            sb.AppendLine("/// vocabulary is required. Returned by <see cref=\"IWorld\"/>'s <c>Query&lt;T0&gt;()</c>.");
            sb.AppendLine("/// There is no separate tracked/untracked overload here — <see cref=\"QueryRow{T0}.Get{T}\"/>");
            sb.AppendLine("/// decides per call, see the design's Mutation and read ergonomics section.");
            sb.AppendLine("/// </summary>");
        }
        else
        {
            sb.AppendLine($"/// <summary>{n}-component overload of <see cref=\"Query{{T0}}\"/>.</summary>");
        }

        sb.AppendLine($"public readonly ref struct Query<{tp}>");
        sb.AppendLine(WhereClauses(n, "    "));
        sb.AppendLine("{");
        sb.AppendLine("    private readonly World _world;");
        foreach (var i in Indices(n))
        {
            sb.AppendLine($"    private readonly int _typeIndex{i};");
            sb.AppendLine($"    private readonly bool _tracked{i};");
        }
        sb.AppendLine("    private readonly int _tick;");
        sb.AppendLine("    private readonly QueryFilter _filter;");
        sb.AppendLine();

        var ctorIndexParams = string.Join(", ", Indices(n).Select(i => $"int typeIndex{i}, bool tracked{i}"));
        sb.AppendLine($"    internal Query(World world, {ctorIndexParams}, int tick, QueryFilter filter)");
        sb.AppendLine("    {");
        sb.AppendLine("        _world = world;");
        foreach (var i in Indices(n))
            sb.AppendLine($"        _typeIndex{i} = typeIndex{i}; _tracked{i} = tracked{i};");
        sb.AppendLine("        _tick = tick;");
        sb.AppendLine("        _filter = filter;");
        sb.AppendLine("    }");
        sb.AppendLine();

        var fieldArgs = string.Join(", ", Indices(n).Select(i => $"_typeIndex{i}, _tracked{i}"));
        var enumCtorArgs = string.Join(", ", Indices(n).Select(i => $"_typeIndex{i}, _tracked{i}"));
        sb.AppendLine(n == 1
            ? "    /// <summary>Returns the enumerator for this query, resolving matching archetypes now (cached — see <see cref=\"World.GetMatchingArchetypes(ArchetypeSignature, QueryFilter)\"/>).</summary>"
            : "    /// <inheritdoc cref=\"Query{T0}.GetEnumerator\"/>");
        sb.AppendLine($"    public Enumerator GetEnumerator() => new(_world.GetMatchingArchetypes(QuerySignature<{tp}>.Value, _filter), {enumCtorArgs}, _tick);");
        sb.AppendLine();

        sb.AppendLine(n == 1
            ? "    /// <summary>Narrows this query to archetypes that also contain <typeparamref name=\"T\"/> — never yields an accessor for it.</summary>"
            : "    /// <inheritdoc cref=\"Query{T0}.Has{T}\"/>");
        sb.AppendLine($"    public Query<{tp}> Has<T>() where T : struct => new(_world, {fieldArgs}, _tick, _filter.Has<T>());");
        sb.AppendLine();

        sb.AppendLine(n == 1
            ? "    /// <summary>Narrows this query to archetypes that do NOT contain <typeparamref name=\"T\"/>.</summary>"
            : "    /// <inheritdoc cref=\"Query{T0}.Without{T}\"/>");
        sb.AppendLine($"    public Query<{tp}> Without<T>() where T : struct => new(_world, {fieldArgs}, _tick, _filter.Without<T>());");
        sb.AppendLine();

        sb.AppendLine(n == 1
            ? "    /// <summary>Narrows this query to archetypes containing at least one of <typeparamref name=\"TA\"/>/<typeparamref name=\"TB\"/>. A second call replaces the first any-of group rather than adding another.</summary>"
            : "    /// <inheritdoc cref=\"Query{T0}.Any{TA,TB}\"/>");
        sb.AppendLine($"    public Query<{tp}> Any<TA, TB>() where TA : struct where TB : struct => new(_world, {fieldArgs}, _tick, _filter.Any<TA, TB>());");
        sb.AppendLine();

        var executeArgs = string.Join(", ", Indices(n).Select(i => $"ref component{i}"));
        sb.AppendLine(n == 1
            ? "    /// <summary>Runs <paramref name=\"action\"/> once per matching entity, sequentially on the calling thread.</summary>"
            : "    /// <inheritdoc cref=\"Query{T0}.ForEach{TUniform}\"/>");
        sb.AppendLine($"    public void ForEach<TUniform>(TUniform uniform, QueryAction<TUniform, {tp}> action)");
        sb.AppendLine("    {");
        sb.AppendLine("        foreach (var row in this)");
        sb.AppendLine("        {");
        foreach (var i in Indices(n))
            sb.AppendLine($"            ref var component{i} = ref row.Get<T{i}>();");
        sb.AppendLine($"            action(uniform, {executeArgs});");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine(n == 1
            ? "    /// <summary>Runs <paramref name=\"action\"/> once per matching entity, fanned out across the thread pool one archetype at a time via <see cref=\"System.Threading.Tasks.Parallel.ForEach{TSource}(IEnumerable{TSource}, Action{TSource})\"/>. The caller is responsible for <paramref name=\"action\"/> being safe to run concurrently with itself and with anything else touching the same World at the same time.</summary>"
            : "    /// <inheritdoc cref=\"Query{T0}.ParallelForEach{TUniform}\"/>");
        sb.AppendLine($"    public void ParallelForEach<TUniform>(TUniform uniform, QueryAction<TUniform, {tp}> action)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var archetypes = _world.GetMatchingArchetypes(QuerySignature<{tp}>.Value, _filter);");
        foreach (var i in Indices(n))
        {
            sb.AppendLine($"        var typeIndex{i} = _typeIndex{i};");
            sb.AppendLine($"        var tracked{i} = _tracked{i};");
        }
        sb.AppendLine("        var tick = _tick;");
        sb.AppendLine("        System.Threading.Tasks.Parallel.ForEach(archetypes, archetype =>");
        sb.AppendLine("        {");
        sb.AppendLine("            if (archetype.Count == 0) return;");
        foreach (var i in Indices(n))
        {
            sb.AppendLine($"            var storage{i} = archetype.Storages[typeIndex{i}];");
            sb.AppendLine($"            var items{i} = ((T{i}[])storage{i}.RawItems).AsSpan(0, archetype.Count);");
            sb.AppendLine($"            var lastMarkedTick{i} = storage{i}.RawLastMarkedTick.AsSpan(0, archetype.Count);");
        }
        sb.AppendLine("            for (var row = 0; row < archetype.Count; row++)");
        sb.AppendLine("            {");
        foreach (var i in Indices(n))
            sb.AppendLine($"                if (tracked{i}) lastMarkedTick{i}[row] = tick;");
        foreach (var i in Indices(n))
            sb.AppendLine($"                ref var component{i} = ref items{i}[row];");
        sb.AppendLine($"                action(uniform, {executeArgs});");
        sb.AppendLine("            }");
        sb.AppendLine("        });");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine(n == 1
            ? $"    /// <summary>Enumerates one <see cref=\"QueryRow{{T0}}\"/> per matching entity.</summary>"
            : "    /// <inheritdoc cref=\"Query{T0}.Enumerator\"/>");
        sb.AppendLine("    public ref struct Enumerator");
        sb.AppendLine("    {");
        sb.AppendLine("        private readonly Archetype[] _archetypes;");
        sb.AppendLine("        private int _archetypeIndex;");
        foreach (var i in Indices(n))
            sb.AppendLine($"        private readonly int _typeIndex{i};");
        foreach (var i in Indices(n))
            sb.AppendLine($"        private readonly bool _tracked{i};");
        sb.AppendLine("        private readonly int _tick;");
        foreach (var i in Indices(n))
        {
            sb.AppendLine($"        private Span<T{i}> _items{i};");
            sb.AppendLine($"        private Span<int> _lastMarkedTick{i};");
        }
        sb.AppendLine("        private Entity[] _entities;");
        sb.AppendLine("        private int _row;");
        sb.AppendLine("        private int _count;");
        sb.AppendLine();

        var enumCtorParams = string.Join(", ", Indices(n).Select(i => $"int typeIndex{i}, bool tracked{i}"));
        sb.AppendLine($"        internal Enumerator(Archetype[] archetypes, {enumCtorParams}, int tick)");
        sb.AppendLine("        {");
        sb.AppendLine("            _archetypes = archetypes;");
        sb.AppendLine("            _archetypeIndex = -1;");
        foreach (var i in Indices(n))
            sb.AppendLine($"            _typeIndex{i} = typeIndex{i}; _tracked{i} = tracked{i};");
        sb.AppendLine("            _tick = tick;");
        foreach (var i in Indices(n))
            sb.AppendLine($"            _items{i} = default; _lastMarkedTick{i} = default;");
        sb.AppendLine("            _entities = Array.Empty<Entity>();");
        sb.AppendLine("            _row = -1;");
        sb.AppendLine("            _count = 0;");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine(n == 1
            ? "        /// <summary>The current row.</summary>"
            : "        /// <inheritdoc cref=\"Query{T0}.Enumerator.Current\"/>");
        var currentArgs = string.Join(", ", Indices(n).Select(i => $"_items{i}, _lastMarkedTick{i}, _tracked{i}"));
        sb.AppendLine($"        public QueryRow<{tp}> Current => new({currentArgs}, _tick, _row, _entities[_row]);");
        sb.AppendLine();

        sb.AppendLine(n == 1
            ? "        /// <summary>Advances to the next matching entity, caching a new archetype's storage exactly once per transition. Only walks the archetypes already known to match this query's component set, not every archetype in the world.</summary>"
            : "        /// <inheritdoc cref=\"Query{T0}.Enumerator.MoveNext\"/>");
        sb.AppendLine("        public bool MoveNext()");
        sb.AppendLine("        {");
        sb.AppendLine("            _row++;");
        sb.AppendLine("            while (_row >= _count)");
        sb.AppendLine("            {");
        sb.AppendLine("                _archetypeIndex++;");
        sb.AppendLine("                if (_archetypeIndex >= _archetypes.Length) return false;");
        sb.AppendLine();
        sb.AppendLine("                var archetype = _archetypes[_archetypeIndex];");
        sb.AppendLine("                if (archetype.Count == 0)");
        sb.AppendLine("                {");
        sb.AppendLine("                    _count = 0;");
        sb.AppendLine("                    _row = 0;");
        sb.AppendLine("                    continue;");
        sb.AppendLine("                }");
        sb.AppendLine();
        foreach (var i in Indices(n))
            sb.AppendLine($"                var storage{i} = archetype.Storages[_typeIndex{i}];");
        foreach (var i in Indices(n))
        {
            sb.AppendLine($"                _items{i} = ((T{i}[])storage{i}.RawItems).AsSpan(0, archetype.Count);");
            sb.AppendLine($"                _lastMarkedTick{i} = storage{i}.RawLastMarkedTick.AsSpan(0, archetype.Count);");
        }
        sb.AppendLine("                _entities = archetype.Entities;");
        sb.AppendLine("                _count = archetype.Count;");
        sb.AppendLine("                _row = 0;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            return true;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>Emits the <c>QueryAction&lt;TUniform, T0..TN-1&gt;</c> delegate used by <c>Query{T0..TN-1}.ForEach</c>/<c>ParallelForEach</c> for arity <paramref name="n"/>.</summary>
    internal static string QueryActionDelegate(int n)
    {
        var tp = TypeParams(n);
        var refParams = string.Join(", ", Indices(n).Select(i => $"ref T{i} component{i}"));
        return (n == 1
                ? "/// <summary>Callback shape for <see cref=\"Query{T0}.ForEach{TUniform}\"/>/<see cref=\"Query{T0}.ParallelForEach{TUniform}\"/> — <paramref name=\"uniform\"/> is passed by value, not captured, so no closure allocation is needed per call.</summary>"
                : $"/// <summary>{n}-component overload of <see cref=\"QueryAction{{TUniform, T0}}\"/>.</summary>")
            + $"\npublic delegate void QueryAction<in TUniform, {tp}>(TUniform uniform, {refParams})"
            + $"\n{WhereClauses(n, "    ")};";
    }

    /// <summary>
    /// Emits <c>QuerySignature&lt;T0..TN-1&gt;</c>: the required archetype signature for
    /// a <c>Query&lt;T0..TN-1&gt;</c>, resolved once per closed generic instantiation
    /// (the same pattern <c>TypeIndex&lt;T&gt;</c> uses) so <c>World</c>'s query members
    /// can look up matching archetypes without rebuilding this signature every call.
    /// </summary>
    internal static string QuerySignature(int n)
    {
        var tp = TypeParams(n);
        var sb = new StringBuilder();

        sb.AppendLine(n == 1
            ? "/// <summary>The required archetype signature for a <see cref=\"Query{T0}\"/>.</summary>"
            : $"/// <summary>{n}-component overload of <see cref=\"QuerySignature{{T0}}\"/>.</summary>");
        sb.AppendLine($"internal static class QuerySignature<{tp}>");
        sb.AppendLine(WhereClauses(n, "    "));
        sb.AppendLine("{");
        var withChain = string.Join("", Indices(n).Select(i => $".With(TypeIndex<T{i}>.Value)"));
        sb.AppendLine($"    internal static readonly ArchetypeSignature Value = ArchetypeSignature.Empty{withChain};");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>Emits the <c>IWorld</c> declaration <c>Query&lt;T0..TN-1&gt; Query&lt;T0..TN-1&gt;()</c>.</summary>
    internal static string IWorldMember(int n)
    {
        var tp = TypeParams(n);
        var where = WhereClausesInline(n);
        return n == 1
            ? "    /// <summary>\n"
                + "    /// Unified entity-tier query, one component: a <c>foreach</c>-able sequence of\n"
                + "    /// <see cref=\"QueryRow{T0}\"/>, one per matching entity, with no chunk or\n"
                + "    /// archetype vocabulary required. Replaces <c>QueryMut&lt;T&gt;</c>/\n"
                + "    /// <c>QueryRef&lt;T&gt;</c> outright — see the design's Unified entity-tier query\n"
                + "    /// section. Narrow the result further via the returned <c>Query{T0}</c>'s own\n"
                + "    /// <c>Has</c>/<c>Without</c>/<c>Any</c> fluent methods — there is no filter parameter\n"
                + "    /// here directly, since that would require exposing the internal\n"
                + "    /// <c>QueryFilter</c> type on a public signature.\n"
                + "    /// </summary>\n"
                + $"    Query<{tp}> Query<{tp}>() {where};"
            : $"    /// <inheritdoc cref=\"Query{{T0}}()\"/>\n    Query<{tp}> Query<{tp}>() {where};";
    }

    /// <summary>Emits the <c>World</c> implementation of <see cref="IWorldMember"/>.</summary>
    internal static string WorldMember(int n)
    {
        var tp = TypeParams(n);
        var where = WhereClausesInline(n);
        var ctorArgs = string.Join(", ", Indices(n).Select(i => $"TypeIndex<T{i}>.Value, IsTracked(TypeIndex<T{i}>.Value)"));
        return $"    /// <inheritdoc/>\n" +
               $"    public Query<{tp}> Query<{tp}>() {where} =>\n" +
               $"        new(this, {ctorArgs}, _currentTick, QueryFilter.Empty);";
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

}
