namespace Wyrd.Ecs;

/// <summary>Serializes a <typeparamref name="T"/> relation edge's payload to bytes. Registered against a stable discriminator via <see cref="ComponentCodecRegistry.RegisterRelation{T}"/> — never against <see cref="Internal.TypeIndex{T}"/>, which is not stable across a process restart.</summary>
public delegate byte[] RelationEncoder<T>(T value) where T : struct, IRelation;

/// <summary>Deserializes bytes produced by a matching <see cref="RelationEncoder{T}"/> back into a <typeparamref name="T"/> value.</summary>
public delegate T RelationDecoder<T>(byte[] data) where T : struct, IRelation;
