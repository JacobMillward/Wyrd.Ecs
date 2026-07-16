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
            sb.AppendLine($"    private readonly DirtyLog _dirtyLog{i};");
        }
        sb.AppendLine("    private readonly int _tick;");
        sb.AppendLine("    private readonly int _row;");
        sb.AppendLine("    private readonly Entity _entity;");
        sb.AppendLine();

        sb.AppendLine("    internal QueryRow(");
        sb.AppendLine(string.Join(",\n", Indices(n).Select(i =>
            $"        Span<T{i}> items{i}, Span<int> lastMarkedTick{i}, DirtyLog dirtyLog{i}")));
        sb.AppendLine("        , int tick, int row, Entity entity)");
        sb.AppendLine("    {");
        foreach (var i in Indices(n))
            sb.AppendLine($"        _items{i} = items{i}; _lastMarkedTick{i} = lastMarkedTick{i}; _dirtyLog{i} = dirtyLog{i};");
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
            sb.AppendLine($"            if (_lastMarkedTick{i}[_row] != _tick)");
            sb.AppendLine("            {");
            sb.AppendLine($"                _lastMarkedTick{i}[_row] = _tick;");
            sb.AppendLine($"                _dirtyLog{i}.Entries[_dirtyLog{i}.Count] = new DirtyEntry(_entity, _tick);");
            sb.AppendLine($"                _dirtyLog{i}.Count++;");
            sb.AppendLine("            }");
            sb.AppendLine($"            return ref Unsafe.As<T{i}, T>(ref _items{i}[_row]);");
            sb.AppendLine("        }");
        }
        sb.AppendLine();
        var typeList = string.Join(", ", Indices(n).Select(i => $"{{typeof(T{i})}}"));
        sb.AppendLine($"        throw new InvalidOperationException($\"Get<{{typeof(T)}}>() was called on a QueryRow<{typeList}> — {{typeof(T)}} is not one of its declared component types.\");");
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
        sb.AppendLine("    private readonly Dictionary<ArchetypeSignature, Archetype>.ValueCollection _archetypes;");
        foreach (var i in Indices(n))
            sb.AppendLine($"    private readonly int _typeIndex{i};");
        sb.AppendLine("    private readonly int _tick;");
        sb.AppendLine();

        var ctorIndexParams = string.Join(", ", Indices(n).Select(i => $"int typeIndex{i}"));
        sb.AppendLine($"    internal Query(Dictionary<ArchetypeSignature, Archetype>.ValueCollection archetypes, {ctorIndexParams}, int tick)");
        sb.AppendLine("    {");
        sb.AppendLine("        _archetypes = archetypes;");
        foreach (var i in Indices(n))
            sb.AppendLine($"        _typeIndex{i} = typeIndex{i};");
        sb.AppendLine("        _tick = tick;");
        sb.AppendLine("    }");
        sb.AppendLine();

        var enumCtorArgs = string.Join(", ", Indices(n).Select(i => $"_typeIndex{i}"));
        sb.AppendLine(n == 1
            ? "    /// <summary>Returns the enumerator for this query.</summary>"
            : "    /// <inheritdoc cref=\"Query{T0}.GetEnumerator\"/>");
        sb.AppendLine($"    public Enumerator GetEnumerator() => new(_archetypes, {enumCtorArgs}, _tick);");
        sb.AppendLine();

        sb.AppendLine(n == 1
            ? $"    /// <summary>Enumerates one <see cref=\"QueryRow{{T0}}\"/> per matching entity.</summary>"
            : "    /// <inheritdoc cref=\"Query{T0}.Enumerator\"/>");
        sb.AppendLine("    public ref struct Enumerator");
        sb.AppendLine("    {");
        sb.AppendLine("        private Dictionary<ArchetypeSignature, Archetype>.ValueCollection.Enumerator _archetypes;");
        foreach (var i in Indices(n))
            sb.AppendLine($"        private readonly int _typeIndex{i};");
        sb.AppendLine("        private readonly int _tick;");
        foreach (var i in Indices(n))
        {
            sb.AppendLine($"        private Span<T{i}> _items{i};");
            sb.AppendLine($"        private Span<int> _lastMarkedTick{i};");
            sb.AppendLine($"        private DirtyLog _dirtyLog{i};");
        }
        sb.AppendLine("        private Entity[] _entities;");
        sb.AppendLine("        private int _row;");
        sb.AppendLine("        private int _count;");
        sb.AppendLine();

        var enumCtorParams = string.Join(", ", Indices(n).Select(i => $"int typeIndex{i}"));
        sb.AppendLine($"        internal Enumerator(Dictionary<ArchetypeSignature, Archetype>.ValueCollection archetypes, {enumCtorParams}, int tick)");
        sb.AppendLine("        {");
        sb.AppendLine("            _archetypes = archetypes.GetEnumerator();");
        foreach (var i in Indices(n))
            sb.AppendLine($"            _typeIndex{i} = typeIndex{i};");
        sb.AppendLine("            _tick = tick;");
        foreach (var i in Indices(n))
            sb.AppendLine($"            _items{i} = default; _lastMarkedTick{i} = default; _dirtyLog{i} = null!;");
        sb.AppendLine("            _entities = Array.Empty<Entity>();");
        sb.AppendLine("            _row = -1;");
        sb.AppendLine("            _count = 0;");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine(n == 1
            ? "        /// <summary>The current row.</summary>"
            : "        /// <inheritdoc cref=\"Query{T0}.Enumerator.Current\"/>");
        var currentArgs = string.Join(", ", Indices(n).Select(i => $"_items{i}, _lastMarkedTick{i}, _dirtyLog{i}"));
        sb.AppendLine($"        public QueryRow<{tp}> Current => new({currentArgs}, _tick, _row, _entities[_row]);");
        sb.AppendLine();

        sb.AppendLine(n == 1
            ? "        /// <summary>Advances to the next matching entity, caching a new archetype's storage exactly once per transition.</summary>"
            : "        /// <inheritdoc cref=\"Query{T0}.Enumerator.MoveNext\"/>");
        sb.AppendLine("        public bool MoveNext()");
        sb.AppendLine("        {");
        sb.AppendLine("            _row++;");
        sb.AppendLine("            while (_row >= _count)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (!_archetypes.MoveNext()) return false;");
        sb.AppendLine();
        sb.AppendLine("                var archetype = _archetypes.Current;");
        sb.Append("                if (archetype.Count == 0");
        foreach (var i in Indices(n))
            sb.Append($" || !archetype.Signature.Contains(_typeIndex{i})");
        sb.AppendLine(")");
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
            sb.AppendLine($"                _dirtyLog{i} = storage{i}.GetDirtyLogForChunk(archetype.Entities, archetype.Count);");
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
                + "    /// section.\n"
                + "    /// </summary>\n"
                + $"    Query<{tp}> Query<{tp}>() {where};"
            : $"    /// <inheritdoc cref=\"Query{{T0}}()\"/>\n    Query<{tp}> Query<{tp}>() {where};";
    }

    /// <summary>Emits the <c>World</c> implementation of <see cref="IWorldMember"/>.</summary>
    internal static string WorldMember(int n)
    {
        var tp = TypeParams(n);
        var where = WhereClausesInline(n);
        var indexArgs = string.Join(", ", Indices(n).Select(i => $"TypeIndex<T{i}>.Value"));
        return $"    /// <inheritdoc/>\n" +
               $"    public Query<{tp}> Query<{tp}>() {where} =>\n" +
               $"        new(_archetypes.Values, {indexArgs}, _currentTick);";
    }
}
