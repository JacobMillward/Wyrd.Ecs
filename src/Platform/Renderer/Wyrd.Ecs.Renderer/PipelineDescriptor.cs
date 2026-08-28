using SDL3;

namespace Wyrd.Ecs.Renderer;

/// <summary>
/// Everything <see cref="RendererSystem"/> needs to build any pipeline for one
/// <see cref="ShaderKind"/>, independent of <see cref="BlendMode"/> (blend/depth state is
/// applied on top by the pipeline cache, see <see cref="PipelineKey"/>). One registered per
/// known <see cref="ShaderKind"/> at construction; a future shader kind adds one more entry,
/// nothing else.
/// </summary>
internal sealed record PipelineDescriptor(
    string VertexShaderResourceName,
    string FragmentShaderResourceName,
    int VertexShaderNumStorageBuffers,
    int VertexShaderNumUniformBuffers,
    int FragmentShaderNumSamplers,
    SDL.GPUVertexBufferDescription[] VertexBufferDescriptions,
    SDL.GPUVertexAttribute[] VertexAttributes,
    SDL.GPUPrimitiveType PrimitiveType,
    SDL.GPUSamplerCreateInfo SamplerCreateInfo);
