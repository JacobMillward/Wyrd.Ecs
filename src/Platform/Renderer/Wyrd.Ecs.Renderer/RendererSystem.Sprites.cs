using SDL3;

namespace Wyrd.Ecs.Renderer;

public sealed partial class RendererSystem
{
    internal IntPtr SpritePipeline { get; private set; }
    internal IntPtr SpriteSampler { get; private set; }

    private void CreateSpritePipeline()
    {
        var (format, extension) = SDL.GetGPUShaderFormats(Device) switch
        {
            var f when (f & SDL.GPUShaderFormat.SPIRV) != 0 => (SDL.GPUShaderFormat.SPIRV, "spirv"),
            var f when (f & SDL.GPUShaderFormat.MSL) != 0 => (SDL.GPUShaderFormat.MSL, "msl"),
            var f when (f & SDL.GPUShaderFormat.DXIL) != 0 => (SDL.GPUShaderFormat.DXIL, "dxil"),
            _ => throw new InvalidOperationException("No supported GPU shader format available for this device."),
        };

        var vertexShader = CreateShaderFromEmbeddedResource($"Wyrd.Ecs.Renderer.Shaders.UnlitSprite.vert.{extension}", format, SDL.GPUShaderStage.Vertex, numStorageBuffers: 1, numUniformBuffers: 2); // CameraBuffer (slot 0) + BatchBuffer (slot 1) — see UnlitSprite.vert.hlsl
        var fragmentShader = CreateShaderFromEmbeddedResource($"Wyrd.Ecs.Renderer.Shaders.UnlitSprite.frag.{extension}", format, SDL.GPUShaderStage.Fragment, numSamplers: 1);

        var colorTarget = new SDL.GPUColorTargetDescription { Format = SDL.GetGPUSwapchainTextureFormat(Device, _platform.Window) };
        var pipelineCreateInfo = new SDL.GPUGraphicsPipelineCreateInfo
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            PrimitiveType = SDL.GPUPrimitiveType.TriangleStrip,
            TargetInfo = new SDL.GPUGraphicsPipelineTargetInfo { NumColorTargets = 1 },
        };
        SpritePipeline = SDL.CreateGPUGraphicsPipeline(Device, in pipelineCreateInfo, [], [], [colorTarget]);
        if (SpritePipeline == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_CreateGPUGraphicsPipeline (UnlitSprite) failed: {SDL.GetError()}");

        SDL.ReleaseGPUShader(Device, vertexShader);
        SDL.ReleaseGPUShader(Device, fragmentShader);

        var samplerCreateInfo = new SDL.GPUSamplerCreateInfo
        {
            MinFilter = SDL.GPUFilter.Nearest,
            MagFilter = SDL.GPUFilter.Nearest,
            AddressModeU = SDL.GPUSamplerAddressMode.ClampToEdge,
            AddressModeV = SDL.GPUSamplerAddressMode.ClampToEdge,
        };
        SpriteSampler = SDL.CreateGPUSampler(Device, in samplerCreateInfo);
        if (SpriteSampler == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_CreateGPUSampler failed: {SDL.GetError()}");
    }

    private IntPtr CreateShaderFromEmbeddedResource(string resourceName, SDL.GPUShaderFormat format, SDL.GPUShaderStage stage, int numSamplers = 0, int numStorageBuffers = 0, int numUniformBuffers = 0)
    {
        using var stream = typeof(RendererSystem).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded shader resource '{resourceName}' not found.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var code = memory.ToArray();

        var createInfo = new SDL.GPUShaderCreateInfo
        {
            Format = format,
            Stage = stage,
            NumSamplers = (uint)numSamplers,
            NumStorageBuffers = (uint)numStorageBuffers,
            NumUniformBuffers = (uint)numUniformBuffers,
        };
        var shader = SDL.CreateGPUShader(Device, in createInfo, code, "main");
        if (shader == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_CreateGPUShader ('{resourceName}') failed: {SDL.GetError()}");
        return shader;
    }
}
