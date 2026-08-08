using System.Collections.Immutable;
using System.Text;

namespace Wyrd.Ecs.Persistence.Binary.Generators;

/// <summary>
/// Renders a <see cref="PlannedFormatter"/> set into standalone
/// <c>MemoryPackFormatter&lt;T&gt;</c> classes plus a <c>[ModuleInitializer]</c> method
/// registering each with <c>MemoryPackFormatterProvider</c>. Classes never touch the
/// component's own declaration - no <c>partial</c> requirement, verified during design (see
/// the design doc's Design B).
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
                sb.AppendLine($"        writer.WriteValue(value.{member.Name});");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine($"    public override void Deserialize(ref global::MemoryPack.MemoryPackReader reader, scoped ref global::{formatter.TypeDisplayName} value)");
            sb.AppendLine("    {");
            foreach (var member in formatter.Members)
                sb.AppendLine($"        value.{member.Name} = reader.ReadValue<{member.TypeDisplayName}>()!;");
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
