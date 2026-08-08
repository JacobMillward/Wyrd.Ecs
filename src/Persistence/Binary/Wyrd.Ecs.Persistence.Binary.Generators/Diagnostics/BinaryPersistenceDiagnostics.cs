using Microsoft.CodeAnalysis;

namespace Wyrd.Ecs.Persistence.Binary.Generators.Diagnostics;

internal static class BinaryPersistenceDiagnostics
{
    /// <summary>
    /// A component field's type can't be automatically serialized for binary persistence:
    /// an interface, an abstract class, or an unresolved open generic parameter. The two
    /// ways out, named in the message, are hand-writing <c>[MemoryPackable]</c> on the
    /// component (bypasses this generator's formatter entirely) or excluding it with
    /// <c>[PersistenceIgnore]</c>.
    /// </summary>
    internal static readonly DiagnosticDescriptor UnsupportedFieldShape = new(
        id: "WYRD006",
        title: "Component field cannot be automatically serialized for binary persistence",
        messageFormat: "'{0}.{1}' has type '{2}', which cannot be automatically serialized for binary persistence. Mark '{0}' with [MemoryPackable] to hand-write its serializer, or [PersistenceIgnore] to exclude it.",
        category: "Wyrd.Ecs.Persistence.Binary",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
