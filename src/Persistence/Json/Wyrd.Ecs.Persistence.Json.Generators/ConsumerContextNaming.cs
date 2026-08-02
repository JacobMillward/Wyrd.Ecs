using System.Text;

namespace Wyrd.Ecs.Persistence.Json.Generators;

/// <summary>
/// The single naming convention that <see cref="JsonContextEmitTask"/> and
/// <see cref="JsonRegistrationGenerator"/> both derive independently, since neither can
/// see the other's output within the same compilation.
/// </summary>
public static class ConsumerContextNaming
{
    /// <summary>The per-project <c>JsonSerializerContext</c> class name, derived from the compiling project's own assembly name.</summary>
    public static string ContextClassName(string assemblyName) =>
        SanitizeIdentifier(assemblyName) + "JsonPersistenceContext";

    /// <summary>
    /// The unique <c>TypeInfoPropertyName</c> for one component type, derived from its
    /// fully-qualified name: two component structs sharing a simple name in different
    /// namespaces would otherwise make System.Text.Json's generator silently emit
    /// source for only the first one detected.
    /// </summary>
    public static string TypeInfoPropertyName(string fullyQualifiedTypeName) =>
        SanitizeIdentifier(fullyQualifiedTypeName);

    private static string SanitizeIdentifier(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');

        if (sb.Length == 0 || !(char.IsLetter(sb[0]) || sb[0] == '_'))
            sb.Insert(0, '_');

        return sb.ToString();
    }
}
