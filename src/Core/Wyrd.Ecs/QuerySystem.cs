namespace Wyrd.Ecs;

/// <summary>
/// Sugar for the single-query, single-callback case: a subclass overrides
/// <see cref="DefineQuery"/> (the query chain, as a static-shaped declaration read
/// purely at compile time) and declares an `Update` method (matching that shape's
/// components, in canonical order — see the design's "QuerySystem: sugar for the
/// single-query, single-callback case" for why `Update` is name-convention-recognized
/// rather than a real override: a method whose parameter list depends on unpacking an
/// arbitrary `TShape` tuple isn't expressible in C#). The query-chain generator supplies
/// <see cref="EcsSystem.Execute"/>.
///
/// <see cref="DefineQuery"/> is a genuine `protected abstract` member, not a
/// name-convention: its signature (`World` in, `IQuery` out) is fixed regardless of
/// `TShape`, so — unlike `Update` — it doesn't have the expressibility problem above.
/// Omitting it is an ordinary `CS0534` ("does not implement inherited abstract member"),
/// with the standard "Implement abstract class" IDE action every C# editor already
/// provides — no custom diagnostic needed for this half of the sugar.
/// </summary>
public abstract class QuerySystem : EcsSystem
{
    /// <summary>
    /// This system's query chain, as a compile-time-only declaration — the query-chain
    /// generator reads this override's return *expression*'s real type via the semantic
    /// model, never its declared `IQuery` return type, so editing the chain (adding a
    /// component, reordering) only ever touches this method's body.
    /// </summary>
    protected abstract IQuery DefineQuery(World world);
}
