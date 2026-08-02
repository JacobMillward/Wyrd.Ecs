namespace Wyrd.Ecs.Internal;

/// <summary>
/// The required <see cref="ArchetypeSignature"/> for a chunk-callback query over
/// <typeparamref name="TAccess0"/>, cached once per closed generic instantiation so
/// <see cref="World.Query{TAccess0}(ChunkAction{TAccess0})"/> doesn't rebuild it on every call.
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

/// <summary>
/// The <see cref="ArchetypeQuery"/> backing <see cref="World.Query{TAccess0}(ChunkAction{TAccess0})"/>,
/// computed once per closed generic instantiation rather than rebuilt on every call.
/// </summary>
internal static class ChunkQuery<TAccess0> where TAccess0 : struct, IComponentAccessor<TAccess0>, allows ref struct
{
    internal static readonly ArchetypeQuery Value = ArchetypeQuery.Empty.Access<TAccess0>();
}

/// <summary>Two-component overload of <see cref="ChunkQuery{TAccess0}"/>.</summary>
internal static class ChunkQuery<TAccess0, TAccess1>
    where TAccess0 : struct, IComponentAccessor<TAccess0>, allows ref struct
    where TAccess1 : struct, IComponentAccessor<TAccess1>, allows ref struct
{
    internal static readonly ArchetypeQuery Value = ArchetypeQuery.Empty.Access<TAccess0>().Access<TAccess1>();
}
