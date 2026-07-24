using System.Text;

namespace Wyrd.Ecs.Persistence.Json.Generators;

/// <summary>
/// The single naming convention <see cref="JsonContextEmitTask"/> (which materializes
/// the <c>JsonSerializerContext</c> partial class to disk) and
/// <see cref="JsonRegistrationGenerator"/> (which emits code referencing that class's
/// members) both derive independently — neither can see the other's output within the
/// same compilation, so agreement here has to come from using the exact same function,
/// not from coordinating at generation time.
/// </summary>
public static class ConsumerContextNaming
{
    /// <summary>The per-project <c>JsonSerializerContext</c> class name, derived from the compiling project's own assembly name.</summary>
    public static string ContextClassName(string assemblyName) =>
        SanitizeIdentifier(assemblyName) + "JsonPersistenceContext";

    /// <summary>
    /// The unique <c>TypeInfoPropertyName</c> for one component type, derived from its
    /// fully-qualified name. Explicit disambiguation is required for every type, not
    /// just colliding ones: two component structs sharing a simple name in different
    /// namespaces otherwise make System.Text.Json's own generator silently emit source
    /// for only the first one detected (a build warning, not an error).
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
