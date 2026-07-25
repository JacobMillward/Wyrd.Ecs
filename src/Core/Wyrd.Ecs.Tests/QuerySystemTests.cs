namespace Wyrd.Ecs.Tests;

internal struct Energy : IComponent
{
    public float Current;
    public float DrainPerSecond;
}

internal struct Marker : ITag;

// TODO(plan-task-4): migrate to QuerySystem<TShape>

internal struct Transceiver : IComponent
{
    public float Bandwidth;
}

internal struct Outbox : IComponent
{
    public float SendProgress;
}

internal struct Inbox : IComponent
{
    public float ReceiveProgress;
}

// TODO(plan-task-4): migrate to QuerySystem<TShape>

public class QuerySystemTests
{
    // TODO(plan-task-4): migrate to QuerySystem<TShape>
}
