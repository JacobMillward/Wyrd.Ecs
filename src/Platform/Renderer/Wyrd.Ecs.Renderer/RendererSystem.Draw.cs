using System.Numerics;
using SDL3;

namespace Wyrd.Ecs.Renderer;

public sealed partial class RendererSystem
{
    private static readonly ArchetypeQuery PerspectiveCameraArchetypeQuery = ArchetypeQuery.Empty
        .Access<Ref<Transform>>().Access<Ref<PerspectiveCamera>>();
    private static readonly ArchetypeQuery OrthographicCameraArchetypeQuery = ArchetypeQuery.Empty
        .Access<Ref<Transform>>().Access<Ref<OrthographicCamera>>();

    private readonly List<(Entity CameraEntity, int Order, bool ClearOnBegin, Matrix4x4 ViewProjection, bool IsOrthographic)> _cameraScratch = [];

    /// <summary>
    /// Renders every active <c>(Transform, PerspectiveCamera)</c> and <c>(Transform,
    /// OrthographicCamera)</c> into <paramref name="swapchainTexture"/>, merged and sorted by
    /// <c>Order</c>. Dispatches each camera to <see cref="DrawSprites"/> or <see cref="DrawMeshes"/>
    /// by which query it came from (the spec's "Sprite entities draw under orthographic cameras;
    /// MeshRenderer entities draw under perspective cameras"). Each owns its own
    /// copy-pass-then-render-pass pair, since <c>ClearOnBegin</c> needs a per-camera render-pass
    /// <c>LoadOp</c> (fixed for a render pass's whole lifetime in SDL_GPU) and the two kinds never
    /// share a render pass anyway (a camera is one or the other, never both). Falls back to a
    /// single clear-and-present pass if there are no active cameras. Called from <see cref="Execute"/>,
    /// only when the swapchain acquire actually succeeded.
    /// </summary>
    private void DrawFrame(World world, IntPtr commandBuffer, IntPtr swapchainTexture, int viewportWidth, int viewportHeight)
    {
        ResolveSprites(world);
        ResolveMeshes(world);

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
                var viewProjection = camera.GetViewMatrix(cameraWorldTransform) * camera.GetProjectionMatrix(aspectRatio);
                _cameraScratch.Add((entities[i], camera.Order, camera.ClearOnBegin, viewProjection, IsOrthographic: false));
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
                var viewProjection = camera.GetViewMatrix(cameraWorldTransform) * camera.GetProjectionMatrix(aspectRatio);
                _cameraScratch.Add((entities[i], camera.Order, camera.ClearOnBegin, viewProjection, IsOrthographic: true));
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
        {
            if (entry.IsOrthographic)
                DrawSprites(world, commandBuffer, swapchainTexture, entry.ClearOnBegin, entry.ViewProjection, viewportWidth, viewportHeight);
            else
                DrawMeshes(world, commandBuffer, swapchainTexture, entry.ClearOnBegin, entry.ViewProjection, viewportWidth, viewportHeight);
        }
    }
}
