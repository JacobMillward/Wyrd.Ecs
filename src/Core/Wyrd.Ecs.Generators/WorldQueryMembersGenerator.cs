using System.Text;
using Microsoft.CodeAnalysis;

namespace Wyrd.Ecs.Generators;

/// <summary>
/// Emits the <c>CommandBuffer.CreateEntity&lt;T0..T{QueryArity.Max-1}&gt;(...)</c>
/// multi-component entity-creation overloads (arity 1 through
/// <see cref="QueryArity.Max"/>), plus <c>World</c>'s internal
/// <c>PlaceReservedEntity&lt;T0..T{QueryArity.Max-1}&gt;</c> helper and the
/// <c>QuerySignature&lt;T0..T{QueryArity.Max-1}&gt;</c> cache it needs to find or
/// create a multi-component entity's target archetype. Query-shape members used to
/// live here too (<c>IWorld</c>/<c>World</c>'s fluent <c>Query&lt;T0..TN-1&gt;()</c>);
/// they were removed when the arity-templated <c>Query&lt;T0,...,T7&gt;</c>/
/// <c>QueryRow&lt;T0,...,T7&gt;</c> family was replaced by the generator-backed
/// unbounded query-shape design — entity creation is an unrelated concern that
/// happened to share this file and <see cref="QueryArity"/>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class WorldQueryMembersGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx =>
        {
            var world = new StringBuilder();
            world.AppendLine("using Wyrd.Ecs.Internal;");
            world.AppendLine();
            world.AppendLine("namespace Wyrd.Ecs;");
            world.AppendLine();
            for (var n = 1; n <= QueryArity.Max; n++)
            {
                world.AppendLine(ArityTemplates.QuerySignature(n));
                world.AppendLine();
            }
            world.AppendLine("public sealed partial class World");
            world.AppendLine("{");
            for (var n = 1; n <= QueryArity.Max; n++)
            {
                world.AppendLine(ArityTemplates.PlaceReservedEntityMember(n));
                world.AppendLine();
            }
            world.AppendLine("}");
            ctx.AddSource("World.QueryMembers.g.cs", world.ToString());

            var commands = new StringBuilder();
            commands.AppendLine("#nullable enable");
            commands.AppendLine("using System;");
            commands.AppendLine("namespace Wyrd.Ecs;");
            commands.AppendLine();
            for (var n = 1; n <= QueryArity.Max; n++)
            {
                commands.AppendLine(ArityTemplates.CreateEntityOpClass(n));
                commands.AppendLine();
            }
            commands.AppendLine("public sealed partial class CommandBuffer");
            commands.AppendLine("{");
            for (var n = 1; n <= QueryArity.Max; n++)
            {
                commands.AppendLine(ArityTemplates.CommandBufferCreateEntityMember(n));
                commands.AppendLine();
            }
            commands.AppendLine("}");
            ctx.AddSource("CommandBuffer.CreateEntityMembers.g.cs", commands.ToString());
        });
    }
}
