using System.Text;
using Microsoft.CodeAnalysis;

namespace Wyrd.Ecs.Generators;

/// <summary>
/// Emits <c>Query&lt;T0..T{QueryArity.Max-1}&gt;</c> and
/// <c>QueryRow&lt;T0..T{QueryArity.Max-1}&gt;</c> (arity 1 through
/// <see cref="QueryArity.Max"/>) directly into the <c>Wyrd.Ecs</c> namespace. Pure
/// template expansion over a fixed arity — this generator never inspects consumer
/// code, unlike the future interceptor generator (a separate, later plan), so a
/// simple <see cref="IIncrementalGenerator.Initialize"/>-time
/// <c>RegisterPostInitializationOutput</c> is sufficient; there is no per-compilation
/// input to react to.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class QueryTypesGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx =>
        {
            var queryRow = new StringBuilder();
            queryRow.AppendLine("using System;");
            queryRow.AppendLine("using System.Runtime.CompilerServices;");
            queryRow.AppendLine();
            queryRow.AppendLine("namespace Wyrd.Ecs;");
            queryRow.AppendLine();
            for (var n = 1; n <= QueryArity.Max; n++)
            {
                queryRow.AppendLine(ArityTemplates.QueryRow(n));
                queryRow.AppendLine();
            }
            ctx.AddSource("QueryRow.g.cs", queryRow.ToString());

            var query = new StringBuilder();
            query.AppendLine("using System;");
            query.AppendLine("using System.Collections.Generic;");
            query.AppendLine("using Wyrd.Ecs.Internal;");
            query.AppendLine();
            query.AppendLine("namespace Wyrd.Ecs;");
            query.AppendLine();
            for (var n = 1; n <= QueryArity.Max; n++)
            {
                query.AppendLine(ArityTemplates.Query(n));
                query.AppendLine();
            }
            ctx.AddSource("Query.g.cs", query.ToString());
        });
    }
}
