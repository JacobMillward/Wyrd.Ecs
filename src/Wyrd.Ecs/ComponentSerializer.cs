namespace Wyrd.Ecs;

/// <summary>Serializes a <typeparamref name="T"/> component's current value to bytes. Registered against a stable discriminator via <see cref="SerializerRegistry.Register{T}"/> — never against <see cref="Internal.TypeIndex{T}"/>, which is not stable across a process restart.</summary>
public delegate byte[] ComponentSerializer<T>(T value) where T : struct, IComponent;

/// <summary>Deserializes bytes produced by a matching <see cref="ComponentSerializer{T}"/> back into a <typeparamref name="T"/> value.</summary>
public delegate T ComponentDeserializer<T>(byte[] data) where T : struct, IComponent;
