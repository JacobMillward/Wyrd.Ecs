using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Wyrd.Ecs.Persistence.Internal;

/// <summary>
/// One nullable, per-<see cref="World"/> value, backed by a <see cref="ConditionalWeakTable{TKey,TValue}"/>
/// so a configured value doesn't outlive the World it was set on. Shared by every
/// extension-member-backed property across the persistence packages (default store,
/// default registry, continuous persistence session), since none of them can add a
/// real field to <see cref="World"/> from another assembly.
/// </summary>
internal sealed class WorldAttachedProperty<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T> where T : class
{
    // The attribute above satisfies ConditionalWeakTable's own trim-analysis requirement on
    // TValue. Get/Set never call GetOrCreateValue, so T's actual constructor shape is irrelevant.
    private readonly ConditionalWeakTable<World, T> _table = new();

    internal T? Get(World world) => _table.TryGetValue(world, out var value) ? value : null;

    internal void Set(World world, T? value)
    {
        if (value is not null) _table.AddOrUpdate(world, value);
        else _table.Remove(world);
    }
}
