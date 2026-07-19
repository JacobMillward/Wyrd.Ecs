using System.Text;
using Microsoft.CodeAnalysis;

namespace Wyrd.Ecs.Generators;

/// <summary>
/// Emits the <c>Query&lt;T0..T{QueryArity.Max-1}&gt;()</c> member declarations on
/// <c>IWorld</c> and their implementations on <c>World</c> (arity 1 through
/// <see cref="QueryArity.Max"/>), as <c>partial interface</c>/<c>partial class</c>
/// additions. Kept separate from <see cref="QueryTypesGenerator"/> — generating the
/// <c>Query&lt;...&gt;</c>/<c>QueryRow&lt;...&gt;</c> type family and generating
/// members onto existing hand-authored types are different concerns with different
/// failure modes (a mistake here breaks <c>IWorld</c>/<c>World</c>'s own compile, not
/// just the generated types).
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class WorldQueryMembersGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx =>
        {
            var iworld = new StringBuilder();
            iworld.AppendLine("namespace Wyrd.Ecs;");
            iworld.AppendLine();
            iworld.AppendLine("public partial interface IWorld");
            iworld.AppendLine("{");
            for (var n = 1; n <= QueryArity.Max; n++)
            {
                iworld.AppendLine(ArityTemplates.IWorldMember(n));
                iworld.AppendLine();
            }
            iworld.AppendLine("}");
            ctx.AddSource("IWorld.QueryMembers.g.cs", iworld.ToString());

            var world = new StringBuilder();
            world.AppendLine("using Wyrd.Ecs.Internal;");
            world.AppendLine();
            world.AppendLine("namespace Wyrd.Ecs;");
            world.AppendLine();
            world.AppendLine("public sealed partial class World");
            world.AppendLine("{");
            for (var n = 1; n <= QueryArity.Max; n++)
            {
                world.AppendLine(ArityTemplates.WorldMember(n));
                world.AppendLine();
            }
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
