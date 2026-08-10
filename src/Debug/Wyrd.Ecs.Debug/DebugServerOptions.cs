namespace Wyrd.Ecs.Debug;

/// <summary>
/// Configuration for <see cref="DebugServer"/>. <see cref="OnError"/> mirrors
/// <c>ContinuousWalWorker</c>'s <c>Action&lt;Exception&gt;? onError</c> constructor
/// parameter: report a failure without assuming any logging story exists or propagating
/// and taking the caller down.
/// </summary>
public sealed record DebugServerOptions(
    int Port = 5299,
    int ChangeLogCapacity = 500,
    Action<Exception>? OnError = null);
