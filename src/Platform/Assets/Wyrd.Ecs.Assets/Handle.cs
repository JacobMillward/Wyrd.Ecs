namespace Wyrd.Ecs.Assets;

/// <summary>
/// A cheap-to-copy reference into an <c>AssetArena&lt;TKey,TAsset&gt;</c> slot, not the asset
/// itself. <typeparamref name="T"/> only distinguishes handle types at compile time (a
/// <c>Handle&lt;Texture&gt;</c> can't be passed where a <c>Handle&lt;Mesh&gt;</c> is expected);
/// it's never inspected at runtime. <see cref="Generation"/> catches use-after-unload: a slot
/// reused by a later <c>Reserve</c> gets a new generation, so a stale handle from before the
/// reuse compares unequal rather than silently resolving to the wrong asset.
/// </summary>
public readonly record struct Handle<T>(int Index, int Generation);
