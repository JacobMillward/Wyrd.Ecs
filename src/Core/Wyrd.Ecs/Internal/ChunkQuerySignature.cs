namespace Wyrd.Ecs.Internal;

/// <summary>
/// The required <see cref="ArchetypeSignature"/> for a chunk-callback query over
/// <typeparamref name="TAccess0"/>, computed once per closed generic instantiation
/// (the same pattern <see cref="TypeIndex{T}"/> already uses: a static field resolved
/// once, the first time this type is touched) so <see cref="World.Query{TAccess0}(ChunkAction{TAccess0})"/>
/// can look up matching archetypes without building this signature on every call.
/// </summary>
internal static class QuerySignature<TAccess0> where TAccess0 : struct, IComponentAccessor<TAccess0>, allows ref struct
{
    internal static readonly ArchetypeSignature Value = ArchetypeSignature.Empty.With(TAccess0.TypeIndex);
}

/// <summary>Two-component overload of <see cref="QuerySignature{TAccess0}"/>.</summary>
internal static class QuerySignature<TAccess0, TAccess1>
    where TAccess0 : struct, IComponentAccessor<TAccess0>, allows ref struct
    where TAccess1 : struct, IComponentAccessor<TAccess1>, allows ref struct
{
    internal static readonly ArchetypeSignature Value = ArchetypeSignature.Empty.With(TAccess0.TypeIndex).With(TAccess1.TypeIndex);
}
