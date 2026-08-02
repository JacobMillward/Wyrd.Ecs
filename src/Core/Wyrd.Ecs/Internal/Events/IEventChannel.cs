namespace Wyrd.Ecs.Internal;

/// <summary>Non-generic hook so <see cref="World"/> can sweep every registered <see cref="EventChannel{T}"/> once per tick without knowing any of their <c>T</c>.</summary>
internal interface IEventChannel
{
    void Swap();
}
