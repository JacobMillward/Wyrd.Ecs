using Microsoft.CodeAnalysis;

namespace Wyrd.Ecs.Persistence.Binary.Generators.Diagnostics;

internal static class BinaryPersistenceDiagnostics
{
    /// <summary>
    /// A component field or property can't be automatically serialized for binary
    /// persistence - its type is an interface, an abstract class, or an unresolved open
    /// generic parameter, or it has no accessible setter (a readonly field or an init-only
    /// property). The third message argument carries the specific cause. The two ways out,
    /// named in the message, are hand-writing <c>[MemoryPackable]</c> on the component
    /// (bypasses this generator's formatter entirely) or excluding it with
    /// <c>[PersistenceIgnore]</c>.
    /// </summary>
    internal static readonly DiagnosticDescriptor UnsupportedFieldShape = new(
        id: "WYRDBIN001",
        title: "Component field cannot be automatically serialized for binary persistence",
        messageFormat: "'{0}.{1}' cannot be automatically serialized for binary persistence: {2}. Mark '{0}' with [MemoryPackable] to hand-write its serializer, or [PersistenceIgnore] to exclude it.",
        category: "Wyrd.Ecs.Persistence.Binary",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
