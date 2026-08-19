using SDL3;

namespace Wyrd.Ecs.Renderer;

/// <summary>
/// One frame-in-flight slot's per-instance storage buffer. Starts at
/// <c>initialCapacity</c> instances, doubles on overflow: allocates a new, larger GPU buffer,
/// writes into it, and retires the old one through <see cref="DeferredDestroyQueue"/> (the
/// same "release once FramesInFlight frames have passed" mechanism already used for
/// texture/mesh unload) so growth never stalls a still-in-flight GPU read of the old buffer.
/// Never shrinks — this is the ceiling <c>SDL_GPU</c> allows (no sparse/reserved resources;
/// see the spec's "Instance buffer growth" and "Decision: staying on SDL_GPU"), not a
/// placeholder for something better later.
/// </summary>
internal sealed class InstanceBuffer
{
    private readonly IntPtr _device;
    private readonly DeferredDestroyQueue _deferredDestroy;
    private IntPtr _buffer;
    private int _capacity;

    public unsafe InstanceBuffer(IntPtr device, DeferredDestroyQueue deferredDestroy, int initialCapacity = 1024)
    {
        _device = device;
        _deferredDestroy = deferredDestroy;
        _capacity = initialCapacity;
        _buffer = Allocate(initialCapacity);
    }

    /// <summary>Writes <paramref name="instances"/> into this slot's buffer (growing first if needed) and returns the live GPU buffer handle to bind for this frame's draw calls.</summary>
    public unsafe IntPtr Write(ReadOnlySpan<SpriteInstanceData> instances, long currentFrame, IntPtr copyPass)
    {
        if (instances.Length > _capacity)
        {
            var newCapacity = _capacity;
            while (newCapacity < instances.Length) newCapacity *= 2;

            var oldBuffer = _buffer;
            var device = _device;
            _deferredDestroy.Enqueue(currentFrame, () => SDL.ReleaseGPUBuffer(device, oldBuffer));

            _buffer = Allocate(newCapacity);
            _capacity = newCapacity;
        }

        if (instances.IsEmpty) return _buffer;

        var elementSize = sizeof(SpriteInstanceData);
        var byteSize = (uint)(instances.Length * elementSize);

        var transferCreateInfo = new SDL.GPUTransferBufferCreateInfo { Usage = SDL.GPUTransferBufferUsage.Upload, Size = byteSize };
        var transferBuffer = SDL.CreateGPUTransferBuffer(_device, in transferCreateInfo);
        var mapped = SDL.MapGPUTransferBuffer(_device, transferBuffer, false);
        fixed (SpriteInstanceData* source = instances)
        {
            Buffer.MemoryCopy(source, (void*)mapped, byteSize, byteSize);
        }
        SDL.UnmapGPUTransferBuffer(_device, transferBuffer);

        var location = new SDL.GPUTransferBufferLocation { TransferBuffer = transferBuffer, Offset = 0 };
        var region = new SDL.GPUBufferRegion { Buffer = _buffer, Offset = 0, Size = byteSize };
        SDL.UploadToGPUBuffer(copyPass, in location, in region, true); // cycle: true avoids overwriting data an in-flight frame may still be reading
        SDL.ReleaseGPUTransferBuffer(_device, transferBuffer);

        return _buffer;
    }

    private unsafe IntPtr Allocate(int capacity)
    {
        var createInfo = new SDL.GPUBufferCreateInfo { Usage = SDL.GPUBufferUsageFlags.GraphicsStorageRead, Size = (uint)(capacity * sizeof(SpriteInstanceData)) };
        var buffer = SDL.CreateGPUBuffer(_device, in createInfo);
        if (buffer == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_CreateGPUBuffer failed: {SDL.GetError()}");
        return buffer;
    }
}
