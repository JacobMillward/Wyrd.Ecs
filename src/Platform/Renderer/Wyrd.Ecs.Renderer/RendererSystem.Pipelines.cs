using System.Runtime.InteropServices;
using SDL3;

namespace Wyrd.Ecs.Renderer;

public sealed partial class RendererSystem
{
    private static readonly SDL.GPUTextureFormat[] DepthStencilFormatPriority =
    [
        SDL.GPUTextureFormat.D32Float,
        SDL.GPUTextureFormat.D24UnormS8Uint,
        SDL.GPUTextureFormat.D16Unorm,
    ];

    /// <summary>
    /// The depth-stencil format every camera's depth texture and every pipeline's
    /// <see cref="SDL.GPUGraphicsPipelineTargetInfo.DepthStencilFormat"/> uses. Chosen once at
    /// construction: neither <see cref="SDL.GPUTextureFormat.D32Float"/> nor
    /// <see cref="SDL.GPUTextureFormat.D24UnormS8Uint"/> is guaranteed across drivers/hardware
    /// (SDL's own docs: always query before using either), so this queries
    /// <see cref="SDL.GPUTextureSupportsFormat"/> in priority order and falls back to the
    /// broadly-supported <see cref="SDL.GPUTextureFormat.D16Unorm"/>.
    /// </summary>
    internal SDL.GPUTextureFormat DepthStencilFormat { get; private set; }

    private static SDL.GPUTextureFormat ChooseDepthStencilFormat(IntPtr device)
    {
        foreach (var format in DepthStencilFormatPriority)
        {
            if (SDL.GPUTextureSupportsFormat(device, format, SDL.GPUTextureType.TextureType2D, SDL.GPUTextureUsageFlags.DepthStencilTarget))
                return format;
        }
        throw new InvalidOperationException("No supported depth-stencil texture format available for this device.");
    }

    private readonly Dictionary<ShaderKind, PipelineDescriptor> _pipelineDescriptors = new();
    private readonly Dictionary<PipelineKey, IntPtr> _pipelines = new();
    private readonly Dictionary<ShaderKind, IntPtr> _samplers = new();
    private SDL.GPUShaderFormat _shaderFormat;
    private string _shaderFileExtension = string.Empty;

    /// <summary>Number of pipelines currently cached. Test-only visibility into <see cref="_pipelines"/>'s size, to assert eager creation without triggering it.</summary>
    internal int PipelineCount => _pipelines.Count;

    /// <summary>
    /// Registers every known <see cref="ShaderKind"/>'s <see cref="PipelineDescriptor"/>,
    /// creates its sampler, and eagerly creates every <see cref="BlendMode"/> variant of its
    /// pipeline. Eager, not lazy: matches the pre-existing guarantee that a pipeline exists
    /// immediately after construction (no first-draw compilation hitch), and there are only
    /// 2 ShaderKinds x 2 BlendModes = 4 pipelines today, cheap to build upfront. Called once
    /// from the constructor, after <see cref="DepthStencilFormat"/> is chosen.
    /// </summary>
    private void CreatePipelines()
    {
        (_shaderFormat, _shaderFileExtension) = ResolveShaderFormat();

        _pipelineDescriptors[ShaderKind.UnlitSprite] = new PipelineDescriptor(
            VertexShaderResourceName: $"Wyrd.Ecs.Renderer.Shaders.UnlitSprite.vert.{_shaderFileExtension}",
            FragmentShaderResourceName: $"Wyrd.Ecs.Renderer.Shaders.UnlitSprite.frag.{_shaderFileExtension}",
            VertexShaderNumStorageBuffers: 1,
            VertexShaderNumUniformBuffers: 2,
            FragmentShaderNumSamplers: 1,
            VertexBufferDescriptions: [],
            VertexAttributes: [],
            PrimitiveType: SDL.GPUPrimitiveType.TriangleStrip,
            SamplerCreateInfo: new SDL.GPUSamplerCreateInfo
            {
                MinFilter = SDL.GPUFilter.Nearest,
                MagFilter = SDL.GPUFilter.Nearest,
                AddressModeU = SDL.GPUSamplerAddressMode.ClampToEdge,
                AddressModeV = SDL.GPUSamplerAddressMode.ClampToEdge,
            });

        _pipelineDescriptors[ShaderKind.UnlitMesh] = new PipelineDescriptor(
            VertexShaderResourceName: $"Wyrd.Ecs.Renderer.Shaders.UnlitMesh.vert.{_shaderFileExtension}",
            FragmentShaderResourceName: $"Wyrd.Ecs.Renderer.Shaders.UnlitMesh.frag.{_shaderFileExtension}",
            VertexShaderNumStorageBuffers: 1,
            VertexShaderNumUniformBuffers: 2,
            FragmentShaderNumSamplers: 1,
            VertexBufferDescriptions:
            [
                new SDL.GPUVertexBufferDescription { Slot = 0, Pitch = (uint)Marshal.SizeOf<MeshVertex>(), InputRate = SDL.GPUVertexInputRate.Vertex, InstanceStepRate = 0 },
            ],
            VertexAttributes:
            [
                new SDL.GPUVertexAttribute { Location = 0, BufferSlot = 0, Format = SDL.GPUVertexElementFormat.Float3, Offset = 0 },  // Position
                new SDL.GPUVertexAttribute { Location = 2, BufferSlot = 0, Format = SDL.GPUVertexElementFormat.Float2, Offset = 24 }, // UV; Location 1 (Normal) omitted, see MeshVertex.cs
            ],
            PrimitiveType: SDL.GPUPrimitiveType.TriangleList,
            SamplerCreateInfo: new SDL.GPUSamplerCreateInfo
            {
                MinFilter = SDL.GPUFilter.Linear,
                MagFilter = SDL.GPUFilter.Linear,
                AddressModeU = SDL.GPUSamplerAddressMode.Repeat,
                AddressModeV = SDL.GPUSamplerAddressMode.Repeat,
            });

        foreach (var (shaderKind, descriptor) in _pipelineDescriptors)
        {
            var samplerCreateInfo = descriptor.SamplerCreateInfo;
            var sampler = SDL.CreateGPUSampler(Device, in samplerCreateInfo);
            if (sampler == IntPtr.Zero)
                throw new InvalidOperationException($"SDL_CreateGPUSampler ('{shaderKind.Name}') failed: {SDL.GetError()}");
            _samplers[shaderKind] = sampler;

            GetOrCreatePipeline(new PipelineKey(shaderKind, BlendMode.Opaque));
            GetOrCreatePipeline(new PipelineKey(shaderKind, BlendMode.Transparent));
        }
    }

    /// <summary>Looks up an already-created pipeline, or builds and caches it. Every combination known at construction is created eagerly by <see cref="CreatePipelines"/>; safe to call again for a key that already exists (used directly by the per-batch draw helpers too).</summary>
    internal IntPtr GetOrCreatePipeline(PipelineKey key)
    {
        if (_pipelines.TryGetValue(key, out var existing))
            return existing;

        var descriptor = _pipelineDescriptors[key.ShaderKind];
        var vertexShader = CreateShaderFromEmbeddedResource(descriptor.VertexShaderResourceName, _shaderFormat, SDL.GPUShaderStage.Vertex, numStorageBuffers: descriptor.VertexShaderNumStorageBuffers, numUniformBuffers: descriptor.VertexShaderNumUniformBuffers);
        var fragmentShader = CreateShaderFromEmbeddedResource(descriptor.FragmentShaderResourceName, _shaderFormat, SDL.GPUShaderStage.Fragment, numSamplers: descriptor.FragmentShaderNumSamplers);

        var colorTarget = new SDL.GPUColorTargetDescription
        {
            Format = SDL.GetGPUSwapchainTextureFormat(Device, _platform.Window),
            BlendState = BuildBlendState(key.BlendMode),
        };
        var pipelineCreateInfo = new SDL.GPUGraphicsPipelineCreateInfo
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            PrimitiveType = descriptor.PrimitiveType,
            DepthStencilState = BuildDepthStencilState(key.BlendMode),
            TargetInfo = new SDL.GPUGraphicsPipelineTargetInfo { NumColorTargets = 1, DepthStencilFormat = DepthStencilFormat },
        };
        var pipeline = SDL.CreateGPUGraphicsPipeline(Device, in pipelineCreateInfo, descriptor.VertexBufferDescriptions, descriptor.VertexAttributes, [colorTarget]);
        if (pipeline == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_CreateGPUGraphicsPipeline ('{key.ShaderKind.Name}', {key.BlendMode}) failed: {SDL.GetError()}");

        SDL.ReleaseGPUShader(Device, vertexShader);
        SDL.ReleaseGPUShader(Device, fragmentShader);

        _pipelines[key] = pipeline;
        return pipeline;
    }

    /// <summary>Binds the pipeline, instance storage buffer, and resolved texture's sampler: the state every batch draw needs regardless of family. Returns the resolved texture so a sprite-family caller can pull its pixel size for <c>BatchUniforms</c>; a mesh-family caller ignores the return value.</summary>
    private Texture BindCommonBatchState(IntPtr renderPass, IntPtr instanceBuffer, Material material)
    {
        var pipeline = GetOrCreatePipeline(new PipelineKey(material.ShaderKind, material.BlendMode));
        SDL.BindGPUGraphicsPipeline(renderPass, pipeline);
        SDL.BindGPUVertexStorageBuffers(renderPass, 0, [instanceBuffer], 1);

        var texture = ResolveTexture(material);
        var samplerBinding = new SDL.GPUTextureSamplerBinding { Texture = texture.GpuTexture, Sampler = _samplers[material.ShaderKind] };
        SDL.BindGPUFragmentSamplers(renderPass, 0, [samplerBinding], 1);
        return texture;
    }

    /// <summary>Pure, no GPU device needed. See <c>PipelineStateTests</c> for direct coverage: this test project has no pixel-readback path, so this is where "is blending actually correct" is actually verified.</summary>
    internal static SDL.GPUColorTargetBlendState BuildBlendState(BlendMode blendMode) => blendMode switch
    {
        BlendMode.Opaque => default,
        BlendMode.Transparent => new SDL.GPUColorTargetBlendState
        {
            EnableBlend = true,
            SrcColorBlendFactor = SDL.GPUBlendFactor.One,
            DstColorBlendFactor = SDL.GPUBlendFactor.OneMinusSrcAlpha,
            ColorBlendOp = SDL.GPUBlendOp.Add,
            SrcAlphaBlendFactor = SDL.GPUBlendFactor.One,
            DstAlphaBlendFactor = SDL.GPUBlendFactor.OneMinusSrcAlpha,
            AlphaBlendOp = SDL.GPUBlendOp.Add,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(blendMode)),
    };

    /// <summary>Pure, no GPU device needed. <see cref="BlendMode.Opaque"/> writes and tests depth (draw order doesn't matter); <see cref="BlendMode.Transparent"/> tests but doesn't write (a transparent fragment shouldn't occlude what's behind another transparent fragment drawn after it).</summary>
    internal static SDL.GPUDepthStencilState BuildDepthStencilState(BlendMode blendMode) => new()
    {
        EnableDepthTest = true,
        EnableDepthWrite = blendMode == BlendMode.Opaque,
        CompareOp = SDL.GPUCompareOp.Less,
    };

    private (SDL.GPUShaderFormat Format, string Extension) ResolveShaderFormat() => SDL.GetGPUShaderFormats(Device) switch
    {
        var f when (f & SDL.GPUShaderFormat.SPIRV) != 0 => (SDL.GPUShaderFormat.SPIRV, "spirv"),
        var f when (f & SDL.GPUShaderFormat.MSL) != 0 => (SDL.GPUShaderFormat.MSL, "msl"),
        var f when (f & SDL.GPUShaderFormat.DXIL) != 0 => (SDL.GPUShaderFormat.DXIL, "dxil"),
        _ => throw new InvalidOperationException("No supported GPU shader format available for this device."),
    };

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
