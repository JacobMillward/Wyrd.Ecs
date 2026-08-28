namespace Wyrd.Ecs.Renderer;

/// <summary>
/// Selects one GPU pipeline: which shader/vertex-layout family (<see cref="ShaderKind"/>) and
/// which blend/depth-write behavior (<see cref="BlendMode"/>). Replaces the old two hardcoded
/// pipeline fields. A future <see cref="ShaderKind"/> registers its own
/// <see cref="PipelineDescriptor"/> (see <see cref="RendererSystem"/>'s pipeline cache) and
/// falls out of this cross product automatically, no new field or method required.
/// </summary>
internal readonly record struct PipelineKey(ShaderKind ShaderKind, BlendMode BlendMode);
