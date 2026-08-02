namespace Wyrd.Ecs;

/// <summary>Serializes a <typeparamref name="T"/> component's current value to bytes. Registered against a stable discriminator via <see cref="ComponentCodecRegistry.Register{T}"/>, never against <see cref="Internal.TypeIndex{T}"/>, which is not stable across a process restart.</summary>
public delegate byte[] ComponentEncoder<T>(T value) where T : struct, IComponent;

/// <summary>Deserializes bytes produced by a matching <see cref="ComponentEncoder{T}"/> back into a <typeparamref name="T"/> value.</summary>
public delegate T ComponentDecoder<T>(byte[] data) where T : struct, IComponent;
