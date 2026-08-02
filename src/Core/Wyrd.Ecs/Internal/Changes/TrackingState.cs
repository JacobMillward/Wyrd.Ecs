namespace Wyrd.Ecs.Internal;

/// <summary>
/// Which component types currently have change tracking turned on, as a ref-counted
/// flat array read — not a dictionary lookup, since <see cref="IsTracked"/> runs on
/// every AddComponent/GetComponent/Query call, the engine's hottest path. A mutable
/// struct, embedded directly in <see cref="World"/> rather than a class, so that
/// hot-path check doesn't pay for an extra heap indirection to reach it.
/// </summary>
internal struct TrackingState
{
    private int[] _consumerCounts = [];

    public TrackingState() { }

    internal bool IsTracked(int typeIndex) => typeIndex < _consumerCounts.Length && _consumerCounts[typeIndex] > 0;

    internal void Register(int typeIndex)
    {
        ArrayGrowth.EnsureCapacity(ref _consumerCounts, typeIndex + 1);
        _consumerCounts[typeIndex]++;
    }

    internal void Unregister(int typeIndex) => _consumerCounts[typeIndex]--;
}
