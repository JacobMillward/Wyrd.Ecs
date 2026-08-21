namespace Wyrd.Ecs.Renderer;

/// <summary>
/// `[Resource]`-injectable asset loading, wrapping <see cref="RendererSystem"/>'s
/// `LoadTexture`/`LoadModel`. Registered automatically by <c>AddRenderer()</c>, so a system
/// wanting to load something declares <c>[Resource] public AssetLoader Assets { get; private
/// set; }</c> (reactive, per-tick loading) or <c>in AssetLoader assets</c> on its constructor
/// (one-time loading, resolved once at construction) instead of calling
/// <c>world.GetSystem&lt;RendererSystem&gt;()</c> directly. <see cref="RendererSystem"/>'s own
/// methods stay public too, this is additive convenience, not a replacement.
/// </summary>
public readonly record struct AssetLoader(RendererSystem Renderer) : IResource
{
    /// <summary>Same as <see cref="RendererSystem.LoadTexture"/>.</summary>
    public Handle<Texture> LoadTexture(string path) => Renderer.LoadTexture(path);

    /// <summary>Same as <see cref="RendererSystem.WaitForLoadAsync(Handle{Texture})"/>.</summary>
    public Task WaitForLoadAsync(Handle<Texture> handle) => Renderer.WaitForLoadAsync(handle);

    /// <summary>Same as <see cref="RendererSystem.Unload(Handle{Texture})"/>.</summary>
    public void Unload(Handle<Texture> handle) => Renderer.Unload(handle);

    /// <summary>Same as <see cref="RendererSystem.LoadModel"/>.</summary>
    public Task<IReadOnlyList<RendererSystem.ModelPart>> LoadModel(string path) => Renderer.LoadModel(path);

    /// <summary>Same as <see cref="RendererSystem.Unload(Handle{Mesh})"/>.</summary>
    public void Unload(Handle<Mesh> handle) => Renderer.Unload(handle);
}
