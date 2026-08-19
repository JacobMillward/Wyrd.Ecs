using System.Numerics;
using SDL3;

namespace Wyrd.Ecs.Renderer;

public sealed partial class RendererSystem
{
    private static readonly ArchetypeQuery CameraArchetypeQuery = ArchetypeQuery.Empty
        .Access<Ref<Transform>>().Access<Ref<Camera>>();

    private readonly List<(Entity CameraEntity, Camera Camera)> _cameraScratch = [];

    /// <summary>
    /// Renders every active <c>(Transform, Camera)</c> into <paramref name="swapchainTexture"/>,
    /// in <see cref="Camera.Order"/> sequence. Dispatches each camera to <see cref="DrawSprites"/>
    /// or <see cref="DrawMeshes"/> by <see cref="ProjectionMode"/> (the spec's "Sprite entities
    /// draw under Orthographic cameras; MeshRenderer entities draw under Perspective cameras").
    /// Each owns its own copy-pass-then-render-pass pair, since <see cref="Camera.ClearOnBegin"/>
    /// needs a per-camera render-pass <c>LoadOp</c> (fixed for a render pass's whole lifetime in
    /// SDL_GPU) and the two kinds never share a render pass anyway (a camera is one or the
    /// other, never both). Falls back to a single clear-and-present pass if there are no active
    /// cameras. Called from <see cref="Execute"/>, only when the swapchain acquire actually
    /// succeeded.
    /// </summary>
    private void DrawFrame(World world, IntPtr commandBuffer, IntPtr swapchainTexture, int viewportWidth, int viewportHeight)
    {
        ResolveSprites(world);
        ResolveMeshes(world);

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

            if (camera.ProjectionMode == ProjectionMode.Orthographic)
                DrawSprites(world, commandBuffer, swapchainTexture, camera, viewProjection, viewportWidth, viewportHeight);
            else
                DrawMeshes(world, commandBuffer, swapchainTexture, camera, viewProjection, viewportWidth, viewportHeight);
        }
    }

    // Stub, replaced once mesh drawing lands; keeps the build green until then.
    private void DrawMeshes(World world, IntPtr commandBuffer, IntPtr swapchainTexture, Camera camera, Matrix4x4 viewProjection, int viewportWidth, int viewportHeight) { }
}
