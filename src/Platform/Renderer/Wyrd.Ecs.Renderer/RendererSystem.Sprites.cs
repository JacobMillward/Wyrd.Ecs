using System.Numerics;
using System.Runtime.InteropServices;
using SDL3;

namespace Wyrd.Ecs.Renderer;

public sealed partial class RendererSystem
{
    internal IntPtr SpritePipeline { get; private set; }
    internal IntPtr SpriteSampler { get; private set; }

    // ArchetypeQuery/ArchetypeChunk (ArchetypeQuery.cs) is the lowest-level query primitive,
    // the only one giving entity-row-aligned Access<TAccessor>() at 3+ component arity without
    // generator setup. World.Query<T0,T1>'s hand-written ChunkAction overloads cap at arity 2
    // and never expose Entity, and the fluent Query<TShape>.ForEach() terminal (generator-
    // emitted) never exposes Entity either. Both ruled out since GetWorldTransform needs it.
    private static readonly ArchetypeQuery SpriteArchetypeQuery = ArchetypeQuery.Empty
        .Access<Ref<Transform>>().Access<Ref<Sprite>>().Access<Ref<Material>>();
    private static readonly ArchetypeQuery CameraArchetypeQuery = ArchetypeQuery.Empty
        .Access<Ref<Transform>>().Access<Ref<Camera>>();

    private readonly InstanceBuffer?[] _instanceBuffersBySlot = new InstanceBuffer?[FrameInFlightTracker.FramesInFlight];
    private readonly SpriteBatcher _spriteBatcher = new();
    private readonly List<(Entity Entity, WorldTransform Transform, Sprite Sprite, Material Material, BoundingSphere Bounds)> _spriteScratch = [];
    private readonly Dictionary<Entity, int> _spriteScratchIndex = new();
    private readonly List<(Entity CameraEntity, Camera Camera)> _cameraScratch = [];
    private readonly List<(Entity Entity, Material Material)> _survivorScratch = [];
    private readonly List<SpriteInstanceData> _instanceScratch = [];
    private readonly List<int> _batchInstanceBases = [];

    [StructLayout(LayoutKind.Sequential)]
    private struct CameraUniforms { public Matrix4x4 ViewProjection; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BatchUniforms { public Vector2 TextureSizePixels; public uint InstanceBase; public uint Padding; }

    /// <summary>
    /// Renders every active <c>(Transform, Camera)</c> into <paramref name="swapchainTexture"/>,
    /// in <see cref="Camera.Order"/> sequence, each in its own copy-pass-then-render-pass pair:
    /// <see cref="Camera.ClearOnBegin"/> needs a per-camera render-pass <c>LoadOp</c> (fixed for
    /// a render pass's whole lifetime in SDL_GPU, so one shared render pass for the frame
    /// can't honor it per camera), and <see cref="SpriteBatcher"/>'s reused storage needs one
    /// camera's batch result fully consumed (bound and drawn) before the next camera's
    /// <c>Batch</c> call reuses the same backing lists. Falls back to a single
    /// clear-and-present pass if there are no active cameras, matching the Foundational
    /// phase's original behavior for that case. Called from <see cref="Execute"/>, only when
    /// the swapchain acquire actually succeeded.
    /// </summary>
    private void DrawFrame(World world, IntPtr commandBuffer, IntPtr swapchainTexture, int viewportWidth, int viewportHeight)
    {
        _spriteScratch.Clear();
        _spriteScratchIndex.Clear();
        foreach (var chunk in SpriteArchetypeQuery.Resolve(world))
        {
            var entities = chunk.Entities;
            var transforms = chunk.Access<Ref<Transform>>();
            var sprites = chunk.Access<Ref<Sprite>>();
            var materials = chunk.Access<Ref<Material>>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var entity = entities[i];
                var sprite = sprites[i];
                var material = materials[i];
                var worldTransform = world.GetWorldTransform(entity);
                var texture = ResolveTexture(material);
                var bounds = SpriteBounds.Compute(worldTransform, sprite, texture.PixelWidth, texture.PixelHeight);
                _spriteScratchIndex[entity] = _spriteScratch.Count;
                _spriteScratch.Add((entity, worldTransform, sprite, material, bounds));
            }
        }

        _cameraScratch.Clear();
        foreach (var chunk in CameraArchetypeQuery.Resolve(world))
        {
            var entities = chunk.Entities;
            var cameras = chunk.Access<Ref<Camera>>();
            for (var i = 0; i < chunk.Count; i++)
                _cameraScratch.Add((entities[i], cameras[i]));
        }
        _cameraScratch.Sort(static (a, b) => a.Camera.Order.CompareTo(b.Camera.Order));

        if (_cameraScratch.Count == 0)
        {
            var colorTarget = new SDL.GPUColorTargetInfo { Texture = swapchainTexture, ClearColor = new SDL.FColor { R = 0f, G = 0f, B = 0f, A = 1f }, LoadOp = SDL.GPULoadOp.Clear, StoreOp = SDL.GPUStoreOp.Store };
            var emptyRenderPass = SDL.BeginGPURenderPass(commandBuffer, [colorTarget], 1, IntPtr.Zero);
            SDL.EndGPURenderPass(emptyRenderPass);
            return;
        }

        foreach (var (cameraEntity, camera) in _cameraScratch)
        {
            var cameraWorldTransform = world.GetWorldTransform(cameraEntity);
            var viewProjection = camera.GetViewMatrix(cameraWorldTransform) * camera.GetProjectionMatrix((float)viewportWidth / viewportHeight);

            _survivorScratch.Clear();
            foreach (var candidate in _spriteScratch)
            {
                if (SpriteBounds.IsInsideFrustum(candidate.Bounds, viewProjection))
                    _survivorScratch.Add((candidate.Entity, candidate.Material));
            }

            var batches = _spriteBatcher.Batch(_survivorScratch);

            _instanceScratch.Clear();
            _batchInstanceBases.Clear();
            foreach (var batch in batches)
            {
                _batchInstanceBases.Add(_instanceScratch.Count);
                foreach (var batchEntity in batch.Entities)
                {
                    var resolved = _spriteScratch[_spriteScratchIndex[batchEntity]]; // already computed once above, no second GetWorldTransform walk
                    var sourceRect = resolved.Sprite.SourceRect is { } r ? new Vector4(r.X, r.Y, r.Width, r.Height) : Vector4.Zero;
                    _instanceScratch.Add(new SpriteInstanceData(resolved.Transform.Position, resolved.Transform.Rotation, resolved.Transform.Scale, resolved.Sprite.Tint, sourceRect));
                }
            }

            var slot = FrameInFlight.SlotIndex;
            var instanceBuffer = _instanceBuffersBySlot[slot] ??= new InstanceBuffer(Device, DeferredDestroy, initialCapacity: 1024);

            var copyPass = SDL.BeginGPUCopyPass(commandBuffer);
            var gpuInstanceBuffer = instanceBuffer.Write(CollectionsMarshal.AsSpan(_instanceScratch), FrameInFlight.CurrentFrame, copyPass);
            SDL.EndGPUCopyPass(copyPass);

            var colorTarget = new SDL.GPUColorTargetInfo
            {
                Texture = swapchainTexture,
                ClearColor = new SDL.FColor { R = 0f, G = 0f, B = 0f, A = 1f },
                LoadOp = camera.ClearOnBegin ? SDL.GPULoadOp.Clear : SDL.GPULoadOp.Load,
                StoreOp = SDL.GPUStoreOp.Store,
            };
            var renderPass = SDL.BeginGPURenderPass(commandBuffer, [colorTarget], 1, IntPtr.Zero);

            SDL.BindGPUGraphicsPipeline(renderPass, SpritePipeline);
            var viewport = new SDL.GPUViewport { X = 0, Y = 0, W = viewportWidth, H = viewportHeight, MinDepth = 0, MaxDepth = 1 };
            SDL.SetGPUViewport(renderPass, in viewport);
            SDL.BindGPUVertexStorageBuffers(renderPass, 0, [gpuInstanceBuffer], 1); // once per camera, same buffer for every batch under it

            var cameraUniforms = new CameraUniforms { ViewProjection = viewProjection };
            var cameraUniformBytes = MemoryMarshal.AsBytes(new ReadOnlySpan<CameraUniforms>(in cameraUniforms));
            SDL.PushGPUVertexUniformData(commandBuffer, 0, cameraUniformBytes, (uint)cameraUniformBytes.Length); // once per camera, unchanging across its batches

            for (var i = 0; i < batches.Count; i++)
            {
                var batch = batches[i];
                var texture = ResolveTexture(batch.Material);

                var samplerBinding = new SDL.GPUTextureSamplerBinding { Texture = texture.GpuTexture, Sampler = SpriteSampler };
                SDL.BindGPUFragmentSamplers(renderPass, 0, [samplerBinding], 1);

                var batchUniforms = new BatchUniforms { TextureSizePixels = new Vector2(texture.PixelWidth, texture.PixelHeight), InstanceBase = (uint)_batchInstanceBases[i] };
                var batchUniformBytes = MemoryMarshal.AsBytes(new ReadOnlySpan<BatchUniforms>(in batchUniforms));
                SDL.PushGPUVertexUniformData(commandBuffer, 1, batchUniformBytes, (uint)batchUniformBytes.Length);

                SDL.DrawGPUPrimitives(renderPass, 4, (uint)batch.Entities.Count, 0, 0); // firstInstance always 0. BatchUniforms.InstanceBase carries the real offset (see UnlitSprite.vert.hlsl)
            }

            SDL.EndGPURenderPass(renderPass);
        }
    }

    private Texture ResolveTexture(Material material) =>
        material.Texture is { } handle && GetTextureLoadState(handle) == LoadState.Loaded
            ? _textureArena.TryGetTexture(handle)!
            : PlaceholderTexture;

    private void CreateSpritePipeline()
    {
        var (format, extension) = SDL.GetGPUShaderFormats(Device) switch
        {
            var f when (f & SDL.GPUShaderFormat.SPIRV) != 0 => (SDL.GPUShaderFormat.SPIRV, "spirv"),
            var f when (f & SDL.GPUShaderFormat.MSL) != 0 => (SDL.GPUShaderFormat.MSL, "msl"),
            var f when (f & SDL.GPUShaderFormat.DXIL) != 0 => (SDL.GPUShaderFormat.DXIL, "dxil"),
            _ => throw new InvalidOperationException("No supported GPU shader format available for this device."),
        };

        var vertexShader = CreateShaderFromEmbeddedResource($"Wyrd.Ecs.Renderer.Shaders.UnlitSprite.vert.{extension}", format, SDL.GPUShaderStage.Vertex, numStorageBuffers: 1, numUniformBuffers: 2); // CameraBuffer (slot 0) + BatchBuffer (slot 1), see UnlitSprite.vert.hlsl
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
