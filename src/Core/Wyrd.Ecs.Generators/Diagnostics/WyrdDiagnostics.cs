using Microsoft.CodeAnalysis;

namespace Wyrd.Ecs.Generators.Diagnostics;

internal static class WyrdDiagnostics
{
    internal static readonly DiagnosticDescriptor BareDataParameter = new(
        id: "WYRD001",
        title: "Query data parameter must be ref or in",
        messageFormat: "Parameter '{0}' must be declared 'ref' or 'in' to specify whether this query writes or reads '{1}'",
        category: "Wyrd.Ecs.QueryChain",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
