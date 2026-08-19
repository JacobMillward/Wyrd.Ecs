using SDL3;

namespace Wyrd.Ecs.Renderer;

/// <summary>
/// One frame-in-flight slot's per-instance storage buffer for instance type <typeparamref name="T"/>
/// (<see cref="SpriteInstanceData"/> or <see cref="MeshInstanceData"/>). Starts at
/// <c>initialCapacity</c> instances, doubles on overflow: allocates a new, larger GPU buffer,
/// writes into it, and retires the old one through <see cref="DeferredDestroyQueue"/> so growth
/// never stalls a still-in-flight GPU read of the old buffer. Never shrinks: see the design
/// spec's "Instance buffer growth" and "Decision: staying on SDL_GPU".
/// </summary>
internal sealed unsafe class InstanceBuffer<T>(IntPtr device, DeferredDestroyQueue deferredDestroy, int initialCapacity = 1024) where T : unmanaged
{
    private int _capacity = initialCapacity;
    private IntPtr _buffer = Allocate(device, initialCapacity);

    /// <summary>Writes <paramref name="instances"/> into this slot's buffer (growing first if needed) and returns the live GPU buffer handle to bind for this frame's draw calls.</summary>
    public IntPtr Write(ReadOnlySpan<T> instances, long currentFrame, IntPtr copyPass)
    {
        if (instances.Length > _capacity)
        {
            var newCapacity = _capacity;
            while (newCapacity < instances.Length) newCapacity *= 2;

            var oldBuffer = _buffer;
            deferredDestroy.Enqueue(currentFrame, () => SDL.ReleaseGPUBuffer(device, oldBuffer));

            _buffer = Allocate(device, newCapacity);
            _capacity = newCapacity;
        }

        if (instances.IsEmpty) return _buffer;

        var elementSize = sizeof(T);
        var byteSize = (uint)(instances.Length * elementSize);

        var transferCreateInfo = new SDL.GPUTransferBufferCreateInfo { Usage = SDL.GPUTransferBufferUsage.Upload, Size = byteSize };
        var transferBuffer = SDL.CreateGPUTransferBuffer(device, in transferCreateInfo);
        var mapped = SDL.MapGPUTransferBuffer(device, transferBuffer, false);
        fixed (T* source = instances)
        {
            Buffer.MemoryCopy(source, (void*)mapped, byteSize, byteSize);
        }
        SDL.UnmapGPUTransferBuffer(device, transferBuffer);

        var location = new SDL.GPUTransferBufferLocation { TransferBuffer = transferBuffer, Offset = 0 };
        var region = new SDL.GPUBufferRegion { Buffer = _buffer, Offset = 0, Size = byteSize };
        SDL.UploadToGPUBuffer(copyPass, in location, in region, true); // cycle: true avoids overwriting data an in-flight frame may still be reading
        SDL.ReleaseGPUTransferBuffer(device, transferBuffer);

        return _buffer;
    }

    private static IntPtr Allocate(IntPtr device, int capacity)
    {
        var createInfo = new SDL.GPUBufferCreateInfo { Usage = SDL.GPUBufferUsageFlags.GraphicsStorageRead, Size = (uint)(capacity * sizeof(T)) };
        var buffer = SDL.CreateGPUBuffer(device, in createInfo);
        if (buffer == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_CreateGPUBuffer failed: {SDL.GetError()}");
        return buffer;
    }
}
