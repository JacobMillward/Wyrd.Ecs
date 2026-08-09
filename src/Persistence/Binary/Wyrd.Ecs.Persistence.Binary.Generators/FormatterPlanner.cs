using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Wyrd.Ecs.Persistence.Binary.Generators.Diagnostics;

namespace Wyrd.Ecs.Persistence.Binary.Generators;

/// <summary>
/// Walks a component's public fields and auto-implemented properties, recursively, to
/// determine which types need a hand-generated <c>MemoryPackFormatter&lt;T&gt;</c> (see
/// <see cref="FormatterEmitter"/>) versus which are already handled by MemoryPack itself
/// (unmanaged types, <c>string</c>, arrays/<c>List&lt;T&gt;</c>/<c>Nullable&lt;T&gt;</c>,
/// or a type already marked <c>[MemoryPackable]</c>). A field whose type this can't safely
/// handle - an interface, an abstract class, or an unresolved open generic parameter -
/// produces a <see cref="BinaryPersistenceDiagnostics.UnsupportedFieldShape"/> diagnostic
/// instead of a formatter.
///
/// Operates on Roslyn symbols throughout, unlike
/// <see cref="MemoryPackRegistrationGenerator"/>'s own <c>RegisteredComponentInfo</c>: safe
/// here because a <see cref="Plan"/> call is entirely local to one
/// <c>MemoryPackRegistrationGenerator.TryExtract</c> invocation and never carried across the
/// incremental pipeline's own caching boundary. <see cref="PlannedFormatter"/>/
/// <see cref="PlannedMember"/>, which the caller does carry through <c>Collect()</c>, store
/// only strings.
/// </summary>
internal static class FormatterPlanner
{
    public static (ImmutableArray<PlannedFormatter> Formatters, ImmutableArray<Diagnostic> Diagnostics) Plan(INamedTypeSymbol componentType, Location diagnosticLocation)
    {
        var terminal = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var queue = new Queue<INamedTypeSymbol>();
        var formatters = ImmutableArray.CreateBuilder<PlannedFormatter>();
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        terminal.Add(componentType);
        queue.Enqueue(componentType);

        while (queue.Count > 0)
        {
            var type = queue.Dequeue();
            var members = ImmutableArray.CreateBuilder<PlannedMember>();

            foreach (var member in GetSerializableMembers(type))
            {
                var memberType = GetMemberType(member);

                if (!IsWritableFromOutsideTheType(member))
                {
                    diagnostics.Add(Diagnostic.Create(
                        BinaryPersistenceDiagnostics.UnsupportedFieldShape,
                        diagnosticLocation,
                        type.Name, member.Name, "it has no accessible setter (readonly field or init-only property)"));
                    continue;
                }

                if (IsUnsupportedShape(memberType))
                {
                    diagnostics.Add(Diagnostic.Create(
                        BinaryPersistenceDiagnostics.UnsupportedFieldShape,
                        diagnosticLocation,
                        type.Name, member.Name, $"its type '{memberType.ToDisplayString()}' isn't supported"));
                    continue;
                }

                members.Add(new PlannedMember(member.Name, memberType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                ProcessType(memberType, queue, terminal);
            }

            formatters.Add(new PlannedFormatter(type.ToDisplayString(), members.ToImmutable()));
        }

        return (formatters.ToImmutable(), diagnostics.ToImmutable());
    }

    private static void ProcessType(ITypeSymbol type, Queue<INamedTypeSymbol> queue, HashSet<ITypeSymbol> terminal)
    {
        if (!terminal.Add(type)) return;

        if (TryGetRecursableTypeArguments(type, out var typeArguments))
        {
            foreach (var argument in typeArguments) ProcessType(argument, queue, terminal);
            return;
        }

        if (type.IsUnmanagedType) return;
        if (type.SpecialType == SpecialType.System_String) return;
        if (HasMemoryPackableAttribute(type)) return;

        if (type is INamedTypeSymbol named) queue.Enqueue(named);
    }

    private static IEnumerable<ISymbol> GetSerializableMembers(INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers())
        {
            switch (member)
            {
                case IFieldSymbol { DeclaredAccessibility: Accessibility.Public, IsStatic: false, IsConst: false, AssociatedSymbol: null } field:
                    yield return field;
                    break;
                case IPropertySymbol { DeclaredAccessibility: Accessibility.Public, IsStatic: false, IsIndexer: false, GetMethod: not null, SetMethod: not null } property:
                    yield return property;
                    break;
            }
        }
    }

    private static ITypeSymbol GetMemberType(ISymbol member) => member switch
    {
        IFieldSymbol field => field.Type,
        IPropertySymbol property => property.Type,
        _ => throw new System.InvalidOperationException($"Unexpected member kind: {member.Kind}"),
    };

    /// <summary>
    /// A readonly field or an init-only property is discoverable by
    /// <see cref="GetSerializableMembers"/> (its accessibility/staticness look identical to
    /// an ordinary writable member) but can't actually be assigned from a generated
    /// formatter class, a different type than the component itself: <c>value.Field = ...</c>
    /// on a readonly field is CS0191, and on an init-only property is CS8852. Diagnosed via
    /// <see cref="BinaryPersistenceDiagnostics.UnsupportedFieldShape"/> rather than silently
    /// dropped, the same "loud, not silent" contract <see cref="IsUnsupportedShape"/>
    /// enforces for a field's type - dropping it silently would lose that field's data on
    /// load with no signal anything happened.
    /// </summary>
    private static bool IsWritableFromOutsideTheType(ISymbol member) => member switch
    {
        IFieldSymbol field => !field.IsReadOnly,
        IPropertySymbol property => property.SetMethod is { IsInitOnly: false },
        _ => false,
    };

    private static bool IsUnsupportedShape(ITypeSymbol type) =>
        type.TypeKind == TypeKind.Interface ||
        type.TypeKind == TypeKind.TypeParameter ||
        (type is INamedTypeSymbol { IsAbstract: true, TypeKind: TypeKind.Class });

    private static bool TryGetRecursableTypeArguments(ITypeSymbol type, out ImmutableArray<ITypeSymbol> typeArguments)
    {
        if (type is IArrayTypeSymbol array)
        {
            typeArguments = ImmutableArray.Create(array.ElementType);
            return true;
        }

        if (type is INamedTypeSymbol { IsGenericType: true } named)
        {
            var definition = named.ConstructedFrom.ToDisplayString();
            if (definition is "System.Collections.Generic.List<T>" or "System.Nullable<T>")
            {
                typeArguments = named.TypeArguments;
                return true;
            }
        }

        typeArguments = ImmutableArray<ITypeSymbol>.Empty;
        return false;
    }

    private static bool HasMemoryPackableAttribute(ITypeSymbol symbol) =>
        symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "MemoryPack.MemoryPackableAttribute");
}

internal record struct PlannedMember(string Name, string TypeDisplayName);

internal record struct PlannedFormatter(string TypeDisplayName, ImmutableArray<PlannedMember> Members);
