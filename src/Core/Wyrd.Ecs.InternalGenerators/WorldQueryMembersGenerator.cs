using System.Text;
using Microsoft.CodeAnalysis;

namespace Wyrd.Ecs.InternalGenerators;

/// <summary>
/// Emits the multi-component <c>CommandBuffer.CreateEntity&lt;T0..T{ArityCap.Max-1}&gt;(...)</c>
/// overloads, <c>World</c>'s internal <c>PlaceReservedEntity&lt;...&gt;</c> helper, and the
/// <c>QuerySignature&lt;...&gt;</c> cache they use to find or create a target archetype.
/// Also emits <c>Query&lt;TShape&gt;</c>'s arity-2+ <c>With</c>/<c>Without</c>/<c>Has</c>/<c>Any</c>
/// overloads. Entity creation and query-chain overloads are unrelated concerns that happen
/// to share this file and <see cref="ArityCap"/>.
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
            for (var n = 1; n <= ArityCap.Max; n++)
            {
                world.AppendLine(ArityTemplates.QuerySignature(n));
                world.AppendLine();
            }
            world.AppendLine("public sealed partial class World");
            world.AppendLine("{");
            for (var n = 1; n <= ArityCap.Max; n++)
            {
                world.AppendLine(ArityTemplates.PlaceReservedEntityMember(n));
                world.AppendLine();
                world.AppendLine(ArityTemplates.PlaceReservedEntitiesMember(n));
                world.AppendLine();
            }
            world.AppendLine("}");
            ctx.AddSource("World.QueryMembers.g.cs", world.ToString());

            var commands = new StringBuilder();
            commands.AppendLine("#nullable enable");
            commands.AppendLine("using System;");
            commands.AppendLine("namespace Wyrd.Ecs;");
            commands.AppendLine();
            for (var n = 1; n <= ArityCap.Max; n++)
            {
                commands.AppendLine(ArityTemplates.CreateEntityOpClass(n));
                commands.AppendLine();
                commands.AppendLine(ArityTemplates.BatchCreateEntityOpClass(n));
                commands.AppendLine();
            }
            commands.AppendLine("public sealed partial class CommandBuffer");
            commands.AppendLine("{");
            for (var n = 1; n <= ArityCap.Max; n++)
            {
                commands.AppendLine(ArityTemplates.CommandBufferCreateEntityMember(n));
                commands.AppendLine();
                commands.AppendLine(ArityTemplates.CommandBufferBatchCreateEntityMember(n));
                commands.AppendLine();
            }
            commands.AppendLine("}");
            ctx.AddSource("CommandBuffer.CreateEntityMembers.g.cs", commands.ToString());

            var query = new StringBuilder();
            query.AppendLine("namespace Wyrd.Ecs;");
            query.AppendLine();
            query.AppendLine("public readonly partial struct Query<TShape> where TShape : struct");
            query.AppendLine("{");
            for (var n = 2; n <= ArityCap.Max; n++)
            {
                query.AppendLine(ArityTemplates.QueryWithMember(n));
                query.AppendLine();
                query.AppendLine(ArityTemplates.QueryWithoutMember(n));
                query.AppendLine();
                query.AppendLine(ArityTemplates.QueryHasMember(n));
                query.AppendLine();
            }
            for (var n = 3; n <= ArityCap.Max; n++) // arity 2 Any<T0,T1> already exists by hand in Query.cs
            {
                query.AppendLine(ArityTemplates.QueryAnyMember(n));
                query.AppendLine();
            }
            query.AppendLine("}");
            ctx.AddSource("Query.ArityMembers.g.cs", query.ToString());

            var queryEntry = new StringBuilder();
            queryEntry.AppendLine("namespace Wyrd.Ecs;");
            queryEntry.AppendLine();
            queryEntry.AppendLine("public readonly partial struct Query");
            queryEntry.AppendLine("{");
            for (var n = 2; n <= ArityCap.Max; n++)
            {
                queryEntry.AppendLine(ArityTemplates.QueryEntryWithMember(n));
                queryEntry.AppendLine();
                queryEntry.AppendLine(ArityTemplates.QueryEntryWithoutMember(n));
                queryEntry.AppendLine();
                queryEntry.AppendLine(ArityTemplates.QueryEntryHasMember(n));
                queryEntry.AppendLine();
            }
            for (var n = 3; n <= ArityCap.Max; n++) // arity 2 Any<T0,T1> already exists by hand in Query.cs
            {
                queryEntry.AppendLine(ArityTemplates.QueryEntryAnyMember(n));
                queryEntry.AppendLine();
            }
            queryEntry.AppendLine("}");
            ctx.AddSource("Query.EntryArityMembers.g.cs", queryEntry.ToString());

            var archetypeQuery = new StringBuilder();
            archetypeQuery.AppendLine("namespace Wyrd.Ecs;");
            archetypeQuery.AppendLine();
            archetypeQuery.AppendLine("public readonly partial struct ArchetypeQuery");
            archetypeQuery.AppendLine("{");
            for (var n = 3; n <= ArityCap.Max; n++)
            {
                archetypeQuery.AppendLine(ArityTemplates.ArchetypeQueryAnyMember(n));
                archetypeQuery.AppendLine();
            }
            archetypeQuery.AppendLine("}");
            ctx.AddSource("ArchetypeQuery.AnyMembers.g.cs", archetypeQuery.ToString());

            var archetypeFilter = new StringBuilder();
            archetypeFilter.AppendLine("using Wyrd.Ecs.Internal;");
            archetypeFilter.AppendLine();
            archetypeFilter.AppendLine("namespace Wyrd.Ecs;");
            archetypeFilter.AppendLine();
            archetypeFilter.AppendLine("public readonly partial struct ArchetypeFilter");
            archetypeFilter.AppendLine("{");
            for (var n = 3; n <= ArityCap.Max; n++)
            {
                archetypeFilter.AppendLine(ArityTemplates.ArchetypeFilterAnyMember(n));
                archetypeFilter.AppendLine();
            }
            archetypeFilter.AppendLine("}");
            ctx.AddSource("ArchetypeFilter.AnyMembers.g.cs", archetypeFilter.ToString());
        });
    }
}
