namespace Wyrd.Ecs.Internal;

/// <summary>
/// The non-generic surface of a <see cref="ChangeConsumer{T}"/> that <see cref="World"/>
/// needs for retention: just the consumer's own advanced tick. Lets <see cref="World"/>
/// keep one registry across every component type without needing to know each
/// consumer's <c>T</c> at the point it computes a trim watermark.
/// </summary>
internal interface IChangeConsumerHandle
{
    int Tick { get; }
}
