namespace Wyrd.Ecs;

/// <summary>
/// Transforms one component's saved bytes from an older schema shape to the next
/// consecutive one. Operates on raw bytes, not a live C# type, because an intermediate
/// shape partway through a migration chain has no corresponding type to be generic
/// over — only the final step's output is ever handed to the real
/// <see cref="ComponentDecoder{T}"/>. Registered via
/// <see cref="ComponentCodecRegistry.RegisterMigration"/>.
/// </summary>
public delegate byte[] SchemaMigrationStep(byte[] oldBytes);
