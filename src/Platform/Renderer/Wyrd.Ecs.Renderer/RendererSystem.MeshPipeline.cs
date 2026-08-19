using SDL3;

namespace Wyrd.Ecs.Renderer;

public sealed partial class RendererSystem
{
    internal IntPtr MeshPipeline { get; private set; }
    internal IntPtr MeshSampler { get; private set; }

    /// <summary>
    /// Real vertex buffer, unlike <c>CreateSpritePipeline</c>'s vertex-id-generated quad:
    /// <see cref="MeshVertex"/>'s 32-byte layout, Position at offset 0 and UV at offset 24.
    /// Location 1 (Normal, offset 12) is deliberately omitted from the attribute array:
    /// <c>UnlitMesh.vert.hlsl</c> never reads <c>input.Normal</c>, so the compiled shader drops
    /// it from its own interface entirely, and SDL_GPU accepts the resulting non-contiguous
    /// {0, 2} attribute-location set. If a future lit shader starts reading <c>Normal</c>, add
    /// its <see cref="SDL.GPUVertexAttribute"/> entry back then.
    /// </summary>
    private unsafe void CreateMeshPipeline()
    {
        var (format, extension) = ResolveShaderFormat();

        var vertexShader = CreateShaderFromEmbeddedResource($"Wyrd.Ecs.Renderer.Shaders.UnlitMesh.vert.{extension}", format, SDL.GPUShaderStage.Vertex, numStorageBuffers: 1, numUniformBuffers: 2);
        var fragmentShader = CreateShaderFromEmbeddedResource($"Wyrd.Ecs.Renderer.Shaders.UnlitMesh.frag.{extension}", format, SDL.GPUShaderStage.Fragment, numSamplers: 1);

        var vertexBufferDescriptions = new[]
        {
            new SDL.GPUVertexBufferDescription { Slot = 0, Pitch = (uint)sizeof(MeshVertex), InputRate = SDL.GPUVertexInputRate.Vertex, InstanceStepRate = 0 },
        };
        var vertexAttributes = new[]
        {
            new SDL.GPUVertexAttribute { Location = 0, BufferSlot = 0, Format = SDL.GPUVertexElementFormat.Float3, Offset = 0 },  // Position
            new SDL.GPUVertexAttribute { Location = 2, BufferSlot = 0, Format = SDL.GPUVertexElementFormat.Float2, Offset = 24 }, // UV
        };

        var colorTarget = new SDL.GPUColorTargetDescription { Format = SDL.GetGPUSwapchainTextureFormat(Device, _platform.Window) };
        var pipelineCreateInfo = new SDL.GPUGraphicsPipelineCreateInfo
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            PrimitiveType = SDL.GPUPrimitiveType.TriangleList,
            TargetInfo = new SDL.GPUGraphicsPipelineTargetInfo { NumColorTargets = 1 },
        };
        MeshPipeline = SDL.CreateGPUGraphicsPipeline(Device, in pipelineCreateInfo, vertexBufferDescriptions, vertexAttributes, [colorTarget]);
        if (MeshPipeline == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_CreateGPUGraphicsPipeline (UnlitMesh) failed: {SDL.GetError()}");

        SDL.ReleaseGPUShader(Device, vertexShader);
        SDL.ReleaseGPUShader(Device, fragmentShader);

        // Linear + repeat, not sprite's nearest + clamp-to-edge: a textured 3D surface, not pixel art.
        var samplerCreateInfo = new SDL.GPUSamplerCreateInfo
        {
            MinFilter = SDL.GPUFilter.Linear,
            MagFilter = SDL.GPUFilter.Linear,
            AddressModeU = SDL.GPUSamplerAddressMode.Repeat,
            AddressModeV = SDL.GPUSamplerAddressMode.Repeat,
        };
        MeshSampler = SDL.CreateGPUSampler(Device, in samplerCreateInfo);
        if (MeshSampler == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_CreateGPUSampler (mesh) failed: {SDL.GetError()}");
    }
}
