using System.Numerics;
using Wyrd.Ecs.Examples.Asteroids.Components;

namespace Wyrd.Ecs.Examples.Asteroids.Systems;

[FixedTimestep]
public sealed partial class SpinSystem : QuerySystem
{
    protected override IQuery DefineQuery(Query query) => query.With<Transform, Spin>();

    public void Update(Time time, ref Transform transform, in Spin spin)
    {
        var delta = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, spin.RadiansPerSecond * (float)time.Delta.TotalSeconds);
        transform.Rotation = delta * transform.Rotation;
    }
}
