using System.Threading;

namespace Wyrd.Ecs.Internal;

internal static class TypeIndexRegistry
{
    private static int _next;

    internal static int Next() => Interlocked.Increment(ref _next) - 1;
}

internal static class TypeIndex<T>
{
    internal static readonly int Value = TypeIndexRegistry.Next();
}
