using System.Numerics;
using System.Runtime.InteropServices;
using SDL3;

namespace Wyrd.Ecs.Renderer;

public sealed partial class RendererSystem
{
    private static readonly ArchetypeQuery PerspectiveCameraArchetypeQuery = ArchetypeQuery.Empty
        .Access<Ref<Transform>>().Access<Ref<PerspectiveCamera>>();
    private static readonly ArchetypeQuery OrthographicCameraArchetypeQuery = ArchetypeQuery.Empty
        .Access<Ref<Transform>>().Access<Ref<OrthographicCamera>>();

    private readonly List<(Entity CameraEntity, int Order, bool ClearOnBegin, Matrix4x4 ViewMatrix, Matrix4x4 ViewProjection)> _cameraScratch = [];

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct CameraUniforms(Matrix4x4 ViewProjection);

    private IntPtr _depthStencilTexture;
    private int _depthStencilWidth;
    private int _depthStencilHeight;

    /// <summary>
    /// (Re)creates the shared depth-stencil texture when the viewport size changes, including
    /// the first call, from zero. One texture, shared across every camera in a frame: each
    /// camera's render pass always clears depth (see <see cref="DrawCamera"/>), so there's
    /// nothing to preserve between cameras and no need for one texture per camera.
    /// </summary>
    private void EnsureDepthStencilTexture(int viewportWidth, int viewportHeight)
    {
        if (_depthStencilTexture != IntPtr.Zero && _depthStencilWidth == viewportWidth && _depthStencilHeight == viewportHeight)
            return;

        if (_depthStencilTexture != IntPtr.Zero)
        {
            var device = Device;
            var stale = _depthStencilTexture;
            DeferredDestroy.Enqueue(FrameInFlight.CurrentFrame, () => SDL.ReleaseGPUTexture(device, stale));
        }

        var createInfo = new SDL.GPUTextureCreateInfo
        {
            Type = SDL.GPUTextureType.TextureType2D,
            Format = DepthStencilFormat,
            Usage = SDL.GPUTextureUsageFlags.DepthStencilTarget,
            Width = (uint)viewportWidth,
            Height = (uint)viewportHeight,
            LayerCountOrDepth = 1,
            NumLevels = 1,
            SampleCount = SDL.GPUSampleCount.SampleCount1,
        };
        _depthStencilTexture = SDL.CreateGPUTexture(Device, in createInfo);
        if (_depthStencilTexture == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_CreateGPUTexture (depth-stencil) failed: {SDL.GetError()}");
        _depthStencilWidth = viewportWidth;
        _depthStencilHeight = viewportHeight;
    }

    /// <summary>
    /// Renders every active <c>(Transform, PerspectiveCamera)</c> and <c>(Transform,
    /// OrthographicCamera)</c> into <paramref name="swapchainTexture"/>, merged and sorted by
    /// <c>Order</c>. Camera-agnostic: either camera type can draw <see cref="Sprite"/> and
    /// <see cref="MeshRenderer"/> entities alike (see <see cref="DrawCamera"/>), unlike the old
    /// ortho-draws-sprites/perspective-draws-meshes coupling. Falls back to a single
    /// clear-and-present pass if there are no active cameras. Called from <see cref="Execute"/>,
    /// only when the swapchain acquire actually succeeded.
    /// </summary>
    private void DrawFrame(World world, IntPtr commandBuffer, IntPtr swapchainTexture, int viewportWidth, int viewportHeight)
    {
        ResolveSprites(world);
        ResolveMeshes(world);
        EnsureDepthStencilTexture(viewportWidth, viewportHeight);

        var aspectRatio = (float)viewportWidth / viewportHeight;

        _cameraScratch.Clear();
        foreach (var chunk in PerspectiveCameraArchetypeQuery.Resolve(world))
        {
            var entities = chunk.Entities;
            var cameras = chunk.Access<Ref<PerspectiveCamera>>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var camera = cameras[i];
                var cameraWorldTransform = world.GetInterpolatedWorldTransform(entities[i]);
                var viewMatrix = camera.GetViewMatrix(cameraWorldTransform);
                var viewProjection = viewMatrix * camera.GetProjectionMatrix(aspectRatio);
                _cameraScratch.Add((entities[i], camera.Order, camera.ClearOnBegin, viewMatrix, viewProjection));
            }
        }
        foreach (var chunk in OrthographicCameraArchetypeQuery.Resolve(world))
        {
            var entities = chunk.Entities;
            var cameras = chunk.Access<Ref<OrthographicCamera>>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var camera = cameras[i];
                var cameraWorldTransform = world.GetInterpolatedWorldTransform(entities[i]);
                var viewMatrix = camera.GetViewMatrix(cameraWorldTransform);
                var viewProjection = viewMatrix * camera.GetProjectionMatrix(aspectRatio);
                _cameraScratch.Add((entities[i], camera.Order, camera.ClearOnBegin, viewMatrix, viewProjection));
            }
        }
        _cameraScratch.Sort(static (a, b) => a.Order.CompareTo(b.Order));

        if (_cameraScratch.Count == 0)
        {
            var colorTarget = new SDL.GPUColorTargetInfo { Texture = swapchainTexture, ClearColor = new SDL.FColor { R = 0f, G = 0f, B = 0f, A = 1f }, LoadOp = SDL.GPULoadOp.Clear, StoreOp = SDL.GPUStoreOp.Store };
            var emptyRenderPass = SDL.BeginGPURenderPass(commandBuffer, [colorTarget], 1, IntPtr.Zero);
            SDL.EndGPURenderPass(emptyRenderPass);
            return;
        }

        foreach (var entry in _cameraScratch)
            DrawCamera(commandBuffer, swapchainTexture, entry.ClearOnBegin, entry.ViewMatrix, entry.ViewProjection, viewportWidth, viewportHeight);
    }

    /// <summary>
    /// Culls both families against <paramref name="viewProjection"/>'s frustum, splits by
    /// <see cref="Material.BlendMode"/>, draws the opaque phase (per-family batched, unordered,
    /// depth write+test), then the transparent phase (both families merged, sorted
    /// back-to-front, depth test only, blend on). One render pass per camera, matching
    /// <see cref="DrawFrame"/>'s per-camera copy-pass-then-render-pass structure.
    /// </summary>
    private void DrawCamera(IntPtr commandBuffer, IntPtr swapchainTexture, bool clearOnBegin, Matrix4x4 viewMatrix, Matrix4x4 viewProjection, int viewportWidth, int viewportHeight)
    {
        _opaqueSpriteScratch.Clear();
        _transparentScratch.Clear();
        foreach (var candidate in _spriteScratch)
        {
            if (!FrustumCulling.IsInsideFrustum(candidate.Bounds, viewProjection)) continue;
            if (candidate.Material.BlendMode == BlendMode.Opaque)
                _opaqueSpriteScratch.Add((candidate.Entity, candidate.Material));
            else
                _transparentScratch.Add((candidate.Entity, new PipelineKey(candidate.Material.ShaderKind, BlendMode.Transparent), candidate.Material, null, Vector3.Transform(candidate.Transform.Position, viewMatrix).Z));
        }

        _opaqueMeshScratch.Clear();
        foreach (var candidate in _meshScratch)
        {
            if (!FrustumCulling.IsInsideFrustum(candidate.Bounds, viewProjection)) continue;
            if (candidate.Material.BlendMode == BlendMode.Opaque)
                _opaqueMeshScratch.Add((candidate.Entity, candidate.Material, candidate.MeshRenderer.Mesh));
            else
                _transparentScratch.Add((candidate.Entity, new PipelineKey(candidate.Material.ShaderKind, BlendMode.Transparent), candidate.Material, candidate.MeshRenderer.Mesh, Vector3.Transform(candidate.Transform.Position, viewMatrix).Z));
        }

        var spriteOpaqueBatches = _spriteBatcher.Batch(_opaqueSpriteScratch);
        var meshOpaqueBatches = _meshBatcher.Batch(_opaqueMeshScratch);
        var transparentBatches = _transparentDrawableBatcher.Batch(_transparentScratch);

        _spriteInstanceScratch.Clear();
        _spriteBatchInstanceBases.Clear();
        foreach (var batch in spriteOpaqueBatches)
        {
            _spriteBatchInstanceBases.Add(_spriteInstanceScratch.Count);
            AppendSpriteInstances(batch.Entities);
        }

        _meshInstanceScratch.Clear();
        _meshBatchInstanceBases.Clear();
        foreach (var batch in meshOpaqueBatches)
        {
            _meshBatchInstanceBases.Add(_meshInstanceScratch.Count);
            AppendMeshInstances(batch.Entities);
        }

        // Transparent batches append to whichever family's scratch list applies (both lists are
        // the same lists the opaque loops above just filled, so one write per family covers
        // both phases). Recorded bases stay valid regardless of draw order: InstanceBase is
        // pushed per batch right before that batch's draw call, so it doesn't matter that draw
        // order (sorted, interleaved) differs from buffer-write order (opaque then transparent,
        // per family). batch.EntityStart/EntityCount slice _transparentDrawableBatcher.Entities.
        var transparentEntities = _transparentDrawableBatcher.Entities;
        _transparentBatchInstanceBases.Clear();
        foreach (var batch in transparentBatches)
        {
            if (batch.Mesh.HasValue) // AppendMeshInstances doesn't need the handle itself: mesh identity only matters at draw time, not for instance data
            {
                _transparentBatchInstanceBases.Add(_meshInstanceScratch.Count);
                AppendMeshInstances(transparentEntities, batch.EntityStart, batch.EntityCount);
            }
            else
            {
                _transparentBatchInstanceBases.Add(_spriteInstanceScratch.Count);
                AppendSpriteInstances(transparentEntities, batch.EntityStart, batch.EntityCount);
            }
        }

        var slot = FrameInFlight.SlotIndex;
        var spriteInstanceBuffer = _instanceBuffersBySlot[slot] ??= new InstanceBuffer<SpriteInstanceData>(Device, DeferredDestroy, initialCapacity: 1024);
        var meshInstanceBuffer = _meshInstanceBuffersBySlot[slot] ??= new InstanceBuffer<MeshInstanceData>(Device, DeferredDestroy, initialCapacity: 256);

        var copyPass = SDL.BeginGPUCopyPass(commandBuffer);
        var gpuSpriteInstanceBuffer = spriteInstanceBuffer.Write(CollectionsMarshal.AsSpan(_spriteInstanceScratch), FrameInFlight.CurrentFrame, copyPass);
        var gpuMeshInstanceBuffer = meshInstanceBuffer.Write(CollectionsMarshal.AsSpan(_meshInstanceScratch), FrameInFlight.CurrentFrame, copyPass);
        SDL.EndGPUCopyPass(copyPass);

        var depthStencilTarget = new SDL.GPUDepthStencilTargetInfo
        {
            Texture = _depthStencilTexture,
            ClearDepth = 1f,
            LoadOp = SDL.GPULoadOp.Clear, // always clear, independent of clearOnBegin: a different camera's depth values are meaningless here (different projection/NDC space)
            StoreOp = SDL.GPUStoreOp.DontCare,
            StencilLoadOp = SDL.GPULoadOp.DontCare,
            StencilStoreOp = SDL.GPUStoreOp.DontCare,
        };
        var colorTarget = new SDL.GPUColorTargetInfo
        {
            Texture = swapchainTexture,
            ClearColor = new SDL.FColor { R = 0f, G = 0f, B = 0f, A = 1f },
            LoadOp = clearOnBegin ? SDL.GPULoadOp.Clear : SDL.GPULoadOp.Load,
            StoreOp = SDL.GPUStoreOp.Store,
        };
        var renderPass = SDL.BeginGPURenderPass(commandBuffer, [colorTarget], 1, in depthStencilTarget);
        var viewport = new SDL.GPUViewport { X = 0, Y = 0, W = viewportWidth, H = viewportHeight, MinDepth = 0, MaxDepth = 1 };
        SDL.SetGPUViewport(renderPass, in viewport);

        var cameraUniforms = new CameraUniforms(viewProjection);
        var cameraUniformBytes = MemoryMarshal.AsBytes(new ReadOnlySpan<CameraUniforms>(in cameraUniforms));
        SDL.PushGPUVertexUniformData(commandBuffer, 0, cameraUniformBytes, (uint)cameraUniformBytes.Length); // once per camera; survives pipeline switches within the phase loop

        for (var i = 0; i < spriteOpaqueBatches.Count; i++)
            DrawSpriteBatch(renderPass, commandBuffer, gpuSpriteInstanceBuffer, _spriteBatchInstanceBases[i], spriteOpaqueBatches[i].Material, spriteOpaqueBatches[i].Entities.Count);
        for (var i = 0; i < meshOpaqueBatches.Count; i++)
            DrawMeshBatch(renderPass, commandBuffer, gpuMeshInstanceBuffer, _meshBatchInstanceBases[i], meshOpaqueBatches[i].Material, meshOpaqueBatches[i].Mesh, meshOpaqueBatches[i].Entities.Count);
        for (var i = 0; i < transparentBatches.Count; i++)
        {
            var batch = transparentBatches[i];
            if (batch.Mesh is { } meshHandle)
                DrawMeshBatch(renderPass, commandBuffer, gpuMeshInstanceBuffer, _transparentBatchInstanceBases[i], batch.Material, meshHandle, batch.EntityCount);
            else
                DrawSpriteBatch(renderPass, commandBuffer, gpuSpriteInstanceBuffer, _transparentBatchInstanceBases[i], batch.Material, batch.EntityCount);
        }

        SDL.EndGPURenderPass(renderPass);
    }
}
