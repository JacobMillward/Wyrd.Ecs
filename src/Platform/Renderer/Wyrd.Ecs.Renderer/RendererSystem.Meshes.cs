using System.Numerics;
using System.Runtime.InteropServices;
using SDL3;

namespace Wyrd.Ecs.Renderer;

public sealed partial class RendererSystem
{
    private readonly MeshArena _meshArena = new();

    private static readonly ArchetypeQuery MeshArchetypeQuery = ArchetypeQuery.Empty
        .Access<Ref<Transform>>().Access<Ref<MeshRenderer>>().Access<Ref<Material>>();

    private readonly List<(Entity Entity, WorldTransform Transform, MeshRenderer MeshRenderer, Material Material, BoundingSphere Bounds)> _meshScratch = [];
    private readonly Dictionary<Entity, int> _meshScratchIndex = new();
    private readonly InstanceBuffer<MeshInstanceData>?[] _meshInstanceBuffersBySlot = new InstanceBuffer<MeshInstanceData>?[FrameInFlightTracker.FramesInFlight];
    private readonly MeshBatcher _meshBatcher = new();
    private readonly List<(Entity Entity, Material Material, Handle<Mesh> Mesh)> _meshSurvivorScratch = [];
    private readonly List<MeshInstanceData> _meshInstanceScratch = [];
    private readonly List<int> _meshBatchInstanceBases = [];
    private readonly List<TaskCompletionSource<IReadOnlyList<ModelPart>>> _modelLoadCompletions = [];

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct MeshBatchUniforms(uint InstanceBase, uint Padding = 0);

    /// <summary>Handle plus optional texture for one Assimp sub-mesh of a loaded model.</summary>
    public readonly record struct ModelPart(Handle<Mesh> Mesh, Handle<Texture>? Texture);

    /// <summary>
    /// Parses <paramref name="path"/> off-thread via <see cref="MeshLoader"/>, reserving one
    /// <see cref="Handle{Mesh}"/> per sub-mesh and starting a background upload for each, the
    /// same way <see cref="LoadTexture"/> works. Unlike <see cref="LoadTexture"/> this returns a
    /// <see cref="Task{TResult}"/> rather than a handle immediately: the part count isn't known
    /// until parsing completes. Each part's texture, if its source material references one, is
    /// loaded through the same <see cref="LoadTexture"/> path; the returned task only waits on
    /// mesh upload, not texture load, matching how a <see cref="Sprite"/>'s texture is allowed
    /// to still be <see cref="LoadState.Loading"/> when the entity is first drawn.
    /// </summary>
    public Task<IReadOnlyList<ModelPart>> LoadModel(string path)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        var completion = new TaskCompletionSource<IReadOnlyList<ModelPart>>();
        _modelLoadCompletions.Add(completion);

        Task.Run(() =>
        {
            try
            {
                var parsed = MeshLoader.Load(path);
                if (parsed.Count == 0)
                {
                    completion.TrySetResult(Array.Empty<ModelPart>());
                    return;
                }

                var parts = new ModelPart[parsed.Count];
                var remaining = parsed.Count;

                for (var i = 0; i < parsed.Count; i++)
                {
                    var index = i;
                    var subMesh = parsed[i];
                    var meshHandle = _meshArena.Reserve(new MeshKey(path, index));
                    var textureHandle = subMesh.TexturePath is { } texturePath ? LoadTexture(texturePath) : (Handle<Texture>?)null;
                    parts[index] = new ModelPart(meshHandle, textureHandle);

                    PendingUploads.Enqueue(copyPass =>
                    {
                        UploadMesh(meshHandle, subMesh.Vertices, subMesh.Indices, copyPass);
                        if (--remaining == 0)
                            completion.TrySetResult(parts);
                    });
                }
            }
            catch (Exception ex)
            {
                PendingUploads.Enqueue(_ => completion.TrySetException(ex));
            }
        });

        return completion.Task;
    }

    /// <summary>Runs on the render thread, inside the copy pass. Creates the GPU vertex/index buffers and uploads via the transfer-buffer/staging pattern, matching <see cref="UploadDecoded"/>'s texture equivalent.</summary>
    private unsafe void UploadMesh(Handle<Mesh> handle, MeshVertex[] vertices, uint[] indices, IntPtr copyPass)
    {
        var vertexByteSize = (uint)(vertices.Length * sizeof(MeshVertex));
        var vertexBufferCreateInfo = new SDL.GPUBufferCreateInfo { Usage = SDL.GPUBufferUsageFlags.Vertex, Size = vertexByteSize };
        var gpuVertexBuffer = SDL.CreateGPUBuffer(Device, in vertexBufferCreateInfo);
        if (gpuVertexBuffer == IntPtr.Zero)
        {
            _meshArena.MarkFailed(handle);
            return;
        }

        var vertexTransferCreateInfo = new SDL.GPUTransferBufferCreateInfo { Usage = SDL.GPUTransferBufferUsage.Upload, Size = vertexByteSize };
        var vertexTransferBuffer = SDL.CreateGPUTransferBuffer(Device, in vertexTransferCreateInfo);
        var mappedVertices = SDL.MapGPUTransferBuffer(Device, vertexTransferBuffer, false);
        fixed (MeshVertex* source = vertices)
            Buffer.MemoryCopy(source, (void*)mappedVertices, vertexByteSize, vertexByteSize);
        SDL.UnmapGPUTransferBuffer(Device, vertexTransferBuffer);
        var vertexSource = new SDL.GPUTransferBufferLocation { TransferBuffer = vertexTransferBuffer, Offset = 0 };
        var vertexDestination = new SDL.GPUBufferRegion { Buffer = gpuVertexBuffer, Offset = 0, Size = vertexByteSize };
        SDL.UploadToGPUBuffer(copyPass, in vertexSource, in vertexDestination, false);
        SDL.ReleaseGPUTransferBuffer(Device, vertexTransferBuffer);

        var indexByteSize = (uint)(indices.Length * sizeof(uint));
        var indexBufferCreateInfo = new SDL.GPUBufferCreateInfo { Usage = SDL.GPUBufferUsageFlags.Index, Size = indexByteSize };
        var gpuIndexBuffer = SDL.CreateGPUBuffer(Device, in indexBufferCreateInfo);
        if (gpuIndexBuffer == IntPtr.Zero)
        {
            SDL.ReleaseGPUBuffer(Device, gpuVertexBuffer);
            _meshArena.MarkFailed(handle);
            return;
        }

        var indexTransferCreateInfo = new SDL.GPUTransferBufferCreateInfo { Usage = SDL.GPUTransferBufferUsage.Upload, Size = indexByteSize };
        var indexTransferBuffer = SDL.CreateGPUTransferBuffer(Device, in indexTransferCreateInfo);
        var mappedIndices = SDL.MapGPUTransferBuffer(Device, indexTransferBuffer, false);
        fixed (uint* source = indices)
            Buffer.MemoryCopy(source, (void*)mappedIndices, indexByteSize, indexByteSize);
        SDL.UnmapGPUTransferBuffer(Device, indexTransferBuffer);
        var indexSource = new SDL.GPUTransferBufferLocation { TransferBuffer = indexTransferBuffer, Offset = 0 };
        var indexDestination = new SDL.GPUBufferRegion { Buffer = gpuIndexBuffer, Offset = 0, Size = indexByteSize };
        SDL.UploadToGPUBuffer(copyPass, in indexSource, in indexDestination, false);
        SDL.ReleaseGPUTransferBuffer(Device, indexTransferBuffer);

        var bounds = MeshBounds.ComputeLocal(vertices);
        _meshArena.MarkLoaded(handle, new Mesh(gpuVertexBuffer, gpuIndexBuffer, (uint)indices.Length, bounds));
    }

    internal LoadState GetMeshLoadState(Handle<Mesh> handle) => _meshArena.GetState(handle);

    /// <summary>Decrements the handle's use-count; once it reaches zero, both GPU buffers are queued on <see cref="DeferredDestroy"/>, released only after <see cref="FrameInFlightTracker.FramesInFlight"/> further frames, same as <see cref="Unload(Handle{Texture})"/>.</summary>
    public void Unload(Handle<Mesh> handle)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        if (!_meshArena.Unload(handle, out var mesh) || mesh is null) return;

        var gpuVertexBuffer = mesh.GpuVertexBuffer;
        var gpuIndexBuffer = mesh.GpuIndexBuffer;
        var device = Device;
        DeferredDestroy.Enqueue(FrameInFlight.CurrentFrame, () =>
        {
            SDL.ReleaseGPUBuffer(device, gpuVertexBuffer);
            SDL.ReleaseGPUBuffer(device, gpuIndexBuffer);
        });
    }

    /// <summary>A unit cube, uploaded synchronously at construction. Drawn in place of any <see cref="Handle{T}"/> still <see cref="LoadState.Loading"/> or gone <see cref="LoadState.Failed"/>, mirroring <see cref="PlaceholderTexture"/>.</summary>
    internal Mesh PlaceholderMesh { get; }

    private unsafe Mesh CreatePlaceholderMesh()
    {
        MeshVertex[] vertices =
        [
            new(new Vector3(-0.5f, -0.5f, -0.5f), Vector3.UnitZ, Vector2.Zero),
            new(new Vector3(0.5f, -0.5f, -0.5f), Vector3.UnitZ, Vector2.Zero),
            new(new Vector3(0.5f, 0.5f, -0.5f), Vector3.UnitZ, Vector2.Zero),
            new(new Vector3(-0.5f, 0.5f, -0.5f), Vector3.UnitZ, Vector2.Zero),
            new(new Vector3(-0.5f, -0.5f, 0.5f), Vector3.UnitZ, Vector2.Zero),
            new(new Vector3(0.5f, -0.5f, 0.5f), Vector3.UnitZ, Vector2.Zero),
            new(new Vector3(0.5f, 0.5f, 0.5f), Vector3.UnitZ, Vector2.Zero),
            new(new Vector3(-0.5f, 0.5f, 0.5f), Vector3.UnitZ, Vector2.Zero),
        ];
        uint[] indices =
        [
            0, 1, 2, 0, 2, 3, // back
            5, 4, 7, 5, 7, 6, // front
            4, 0, 3, 4, 3, 7, // left
            1, 5, 6, 1, 6, 2, // right
            3, 2, 6, 3, 6, 7, // top
            4, 5, 1, 4, 1, 0, // bottom
        ];

        var vertexByteSize = (uint)(vertices.Length * sizeof(MeshVertex));
        var vertexBufferCreateInfo = new SDL.GPUBufferCreateInfo { Usage = SDL.GPUBufferUsageFlags.Vertex, Size = vertexByteSize };
        var gpuVertexBuffer = SDL.CreateGPUBuffer(Device, in vertexBufferCreateInfo);
        if (gpuVertexBuffer == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_CreateGPUBuffer (placeholder mesh vertices) failed: {SDL.GetError()}");

        var indexByteSize = (uint)(indices.Length * sizeof(uint));
        var indexBufferCreateInfo = new SDL.GPUBufferCreateInfo { Usage = SDL.GPUBufferUsageFlags.Index, Size = indexByteSize };
        var gpuIndexBuffer = SDL.CreateGPUBuffer(Device, in indexBufferCreateInfo);
        if (gpuIndexBuffer == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_CreateGPUBuffer (placeholder mesh indices) failed: {SDL.GetError()}");

        var vertexTransferCreateInfo = new SDL.GPUTransferBufferCreateInfo { Usage = SDL.GPUTransferBufferUsage.Upload, Size = vertexByteSize };
        var vertexTransferBuffer = SDL.CreateGPUTransferBuffer(Device, in vertexTransferCreateInfo);
        var mappedVertices = SDL.MapGPUTransferBuffer(Device, vertexTransferBuffer, false);
        fixed (MeshVertex* source = vertices)
            Buffer.MemoryCopy(source, (void*)mappedVertices, vertexByteSize, vertexByteSize);
        SDL.UnmapGPUTransferBuffer(Device, vertexTransferBuffer);

        var indexTransferCreateInfo = new SDL.GPUTransferBufferCreateInfo { Usage = SDL.GPUTransferBufferUsage.Upload, Size = indexByteSize };
        var indexTransferBuffer = SDL.CreateGPUTransferBuffer(Device, in indexTransferCreateInfo);
        var mappedIndices = SDL.MapGPUTransferBuffer(Device, indexTransferBuffer, false);
        fixed (uint* source = indices)
            Buffer.MemoryCopy(source, (void*)mappedIndices, indexByteSize, indexByteSize);
        SDL.UnmapGPUTransferBuffer(Device, indexTransferBuffer);

        var commandBuffer = SDL.AcquireGPUCommandBuffer(Device);
        var copyPass = SDL.BeginGPUCopyPass(commandBuffer);
        var vertexSource = new SDL.GPUTransferBufferLocation { TransferBuffer = vertexTransferBuffer, Offset = 0 };
        var vertexDestination = new SDL.GPUBufferRegion { Buffer = gpuVertexBuffer, Offset = 0, Size = vertexByteSize };
        SDL.UploadToGPUBuffer(copyPass, in vertexSource, in vertexDestination, false);
        var indexSource = new SDL.GPUTransferBufferLocation { TransferBuffer = indexTransferBuffer, Offset = 0 };
        var indexDestination = new SDL.GPUBufferRegion { Buffer = gpuIndexBuffer, Offset = 0, Size = indexByteSize };
        SDL.UploadToGPUBuffer(copyPass, in indexSource, in indexDestination, false);
        SDL.EndGPUCopyPass(copyPass);
        SDL.SubmitGPUCommandBuffer(commandBuffer);
        SDL.ReleaseGPUTransferBuffer(Device, vertexTransferBuffer);
        SDL.ReleaseGPUTransferBuffer(Device, indexTransferBuffer);

        return new Mesh(gpuVertexBuffer, gpuIndexBuffer, (uint)indices.Length, MeshBounds.ComputeLocal(vertices));
    }

    private Mesh ResolveMesh(Handle<Mesh> handle) =>
        GetMeshLoadState(handle) == LoadState.Loaded ? _meshArena.TryGetMesh(handle)! : PlaceholderMesh;

    private void ResolveMeshes(World world)
    {
        _meshScratch.Clear();
        _meshScratchIndex.Clear();
        foreach (var chunk in MeshArchetypeQuery.Resolve(world))
        {
            var entities = chunk.Entities;
            var transforms = chunk.Access<Ref<Transform>>();
            var meshRenderers = chunk.Access<Ref<MeshRenderer>>();
            var materials = chunk.Access<Ref<Material>>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var entity = entities[i];
                var meshRenderer = meshRenderers[i];
                var material = materials[i];
                var worldTransform = world.GetInterpolatedWorldTransform(entity);
                var mesh = ResolveMesh(meshRenderer.Mesh);
                var bounds = MeshBounds.ComputeWorld(worldTransform, mesh.LocalBounds);
                _meshScratchIndex[entity] = _meshScratch.Count;
                _meshScratch.Add((entity, worldTransform, meshRenderer, material, bounds));
            }
        }
    }

    /// <summary>Culls, batches, and draws every <see cref="_meshScratch"/> entry surviving <paramref name="viewProjection"/>'s frustum. Called once per active <see cref="PerspectiveCamera"/>.</summary>
    private void DrawMeshes(World world, IntPtr commandBuffer, IntPtr swapchainTexture, bool clearOnBegin, Matrix4x4 viewProjection, int viewportWidth, int viewportHeight)
    {
        _meshSurvivorScratch.Clear();
        foreach (var candidate in _meshScratch)
        {
            if (FrustumCulling.IsInsideFrustum(candidate.Bounds, viewProjection))
                _meshSurvivorScratch.Add((candidate.Entity, candidate.Material, candidate.MeshRenderer.Mesh));
        }

        var batches = _meshBatcher.Batch(_meshSurvivorScratch);

        _meshInstanceScratch.Clear();
        _meshBatchInstanceBases.Clear();
        foreach (var batch in batches)
        {
            _meshBatchInstanceBases.Add(_meshInstanceScratch.Count);
            foreach (var batchEntity in batch.Entities)
            {
                var resolved = _meshScratch[_meshScratchIndex[batchEntity]];
                _meshInstanceScratch.Add(new MeshInstanceData(resolved.Transform.Position, resolved.Transform.Rotation, resolved.Transform.Scale, resolved.MeshRenderer.Tint));
            }
        }

        var slot = FrameInFlight.SlotIndex;
        var instanceBuffer = _meshInstanceBuffersBySlot[slot] ??= new InstanceBuffer<MeshInstanceData>(Device, DeferredDestroy, initialCapacity: 256);

        var copyPass = SDL.BeginGPUCopyPass(commandBuffer);
        var gpuInstanceBuffer = instanceBuffer.Write(CollectionsMarshal.AsSpan(_meshInstanceScratch), FrameInFlight.CurrentFrame, copyPass);
        SDL.EndGPUCopyPass(copyPass);

        var colorTarget = new SDL.GPUColorTargetInfo
        {
            Texture = swapchainTexture,
            ClearColor = new SDL.FColor { R = 0f, G = 0f, B = 0f, A = 1f },
            LoadOp = clearOnBegin ? SDL.GPULoadOp.Clear : SDL.GPULoadOp.Load,
            StoreOp = SDL.GPUStoreOp.Store,
        };
        var renderPass = SDL.BeginGPURenderPass(commandBuffer, [colorTarget], 1, IntPtr.Zero);

        SDL.BindGPUGraphicsPipeline(renderPass, MeshPipeline);
        var viewport = new SDL.GPUViewport { X = 0, Y = 0, W = viewportWidth, H = viewportHeight, MinDepth = 0, MaxDepth = 1 };
        SDL.SetGPUViewport(renderPass, in viewport);
        SDL.BindGPUVertexStorageBuffers(renderPass, 0, [gpuInstanceBuffer], 1);

        var cameraUniforms = new CameraUniforms(viewProjection);
        var cameraUniformBytes = MemoryMarshal.AsBytes(new ReadOnlySpan<CameraUniforms>(in cameraUniforms));
        SDL.PushGPUVertexUniformData(commandBuffer, 0, cameraUniformBytes, (uint)cameraUniformBytes.Length);

        for (var i = 0; i < batches.Count; i++)
        {
            var batch = batches[i];
            var mesh = ResolveMesh(batch.Mesh);
            var texture = ResolveTexture(batch.Material);

            var vertexBinding = new SDL.GPUBufferBinding { Buffer = mesh.GpuVertexBuffer, Offset = 0 };
            SDL.BindGPUVertexBuffers(renderPass, 0, [vertexBinding], 1);
            var indexBinding = new SDL.GPUBufferBinding { Buffer = mesh.GpuIndexBuffer, Offset = 0 };
            SDL.BindGPUIndexBuffer(renderPass, in indexBinding, SDL.GPUIndexElementSize.IndexElementSize32Bit);

            var samplerBinding = new SDL.GPUTextureSamplerBinding { Texture = texture.GpuTexture, Sampler = MeshSampler };
            SDL.BindGPUFragmentSamplers(renderPass, 0, [samplerBinding], 1);

            var batchUniforms = new MeshBatchUniforms((uint)_meshBatchInstanceBases[i]);
            var batchUniformBytes = MemoryMarshal.AsBytes(new ReadOnlySpan<MeshBatchUniforms>(in batchUniforms));
            SDL.PushGPUVertexUniformData(commandBuffer, 1, batchUniformBytes, (uint)batchUniformBytes.Length);

            SDL.DrawGPUIndexedPrimitives(renderPass, mesh.IndexCount, (uint)batch.Entities.Count, 0, 0, 0); // firstInstance always 0, see MeshBatchUniforms.InstanceBase
        }

        SDL.EndGPURenderPass(renderPass);
    }
}
