using Wyrd.Ecs.Debug.Abstractions;

namespace Wyrd.Ecs.Debug.Internal;

/// <summary>
/// One component as the debug UI sees it: its raw encoded form plus, if
/// <see cref="Component"/>'s discriminator has a registered <c>[DebugRenderer]</c>,
/// the described <see cref="InspectorField"/> tree for it. <see cref="Field"/> is
/// <c>null</c> for every component with no registered renderer, since the generic
/// per-property JSON grid is the frontend's fallback for those.
/// </summary>
internal readonly record struct InspectedComponent(EncodedComponent Component, InspectorField? Field);
