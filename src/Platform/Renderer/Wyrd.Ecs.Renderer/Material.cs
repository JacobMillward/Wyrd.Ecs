namespace Wyrd.Ecs.Renderer;

/// <summary>
/// Pipeline-selecting state only — shader and texture, the two things that must be identical
/// for two entities to share a batched draw call. Deliberately carries no per-instance-varying
/// data (no tint): that lives on <see cref="Sprite"/> instead, so two sprites using the same
/// <see cref="Material"/> with different tints still batch together. See the spec's "2D
/// batching and instancing" for why this split is load-bearing, not stylistic.
/// </summary>
public readonly record struct Material(ShaderKind ShaderKind, Handle<Texture>? Texture) : IComponent;
