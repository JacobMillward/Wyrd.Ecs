using System.Collections.Immutable;
using System.Text;

namespace Wyrd.Ecs.Persistence.Binary.Generators;

/// <summary>
/// Renders a <see cref="PlannedFormatter"/> set into standalone
/// <c>MemoryPackFormatter&lt;T&gt;</c> classes plus a <c>[ModuleInitializer]</c> method
/// registering each with <c>MemoryPackFormatterProvider</c>. Classes never touch the
/// component's own declaration: no <c>partial</c> requirement.
///
/// A <c>string</c> member uses <c>WriteString</c>/<c>ReadString</c> directly rather than
/// the generic <c>WriteValue</c>/<c>ReadValue&lt;T&gt;</c> every other member type uses:
/// roughly 30% faster for serialize (see <c>BinaryPersistenceFormatterBenchmarks</c>), since
/// it skips the runtime <c>MemoryPackFormatterProvider</c> lookup
/// <c>WriteValue&lt;string&gt;</c> pays on every call. <c>string</c> is common enough in
/// component shapes to be worth the one special case.
/// </summary>
internal static class FormatterEmitter
{
    public static void AppendFormatters(StringBuilder sb, ImmutableArray<PlannedFormatter> formatters)
    {
        if (formatters.IsEmpty) return;

        foreach (var formatter in formatters)
        {
            var className = FormatterClassName(formatter.TypeDisplayName);
            sb.AppendLine();
            sb.AppendLine($"internal sealed class {className} : global::MemoryPack.MemoryPackFormatter<global::{formatter.TypeDisplayName}>");
            sb.AppendLine("{");
            sb.AppendLine($"    public override void Serialize<TBufferWriter>(ref global::MemoryPack.MemoryPackWriter<TBufferWriter> writer, scoped ref global::{formatter.TypeDisplayName} value)");
            sb.AppendLine("    {");
            foreach (var member in formatter.Members)
            {
                if (member.TypeDisplayName == "string")
                    sb.AppendLine($"        writer.WriteString(value.{member.Name});");
                else
                    sb.AppendLine($"        writer.WriteValue(value.{member.Name});");
            }
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine($"    public override void Deserialize(ref global::MemoryPack.MemoryPackReader reader, scoped ref global::{formatter.TypeDisplayName} value)");
            sb.AppendLine("    {");
            foreach (var member in formatter.Members)
            {
                if (member.TypeDisplayName == "string")
                    sb.AppendLine($"        value.{member.Name} = reader.ReadString()!;");
                else
                    sb.AppendLine($"        value.{member.Name} = reader.ReadValue<{member.TypeDisplayName}>()!;");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");
        }

        sb.AppendLine();
        sb.AppendLine("internal static class MemoryPackGeneratedFormatterRegistration");
        sb.AppendLine("{");
        sb.AppendLine("    [System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("    internal static void Init()");
        sb.AppendLine("    {");
        foreach (var formatter in formatters)
            sb.AppendLine($"        global::MemoryPack.MemoryPackFormatterProvider.Register(new {FormatterClassName(formatter.TypeDisplayName)}());");
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    private static string FormatterClassName(string typeDisplayName) =>
        typeDisplayName.Replace(".", "_").Replace("<", "_").Replace(">", "_").Replace(",", "_").Replace(" ", "") + "GeneratedFormatter";
}
