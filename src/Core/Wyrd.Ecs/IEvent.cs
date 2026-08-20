namespace Wyrd.Ecs;

/// <summary>
/// Marker interface for event structs: a typed value emitted via <see cref="World.Emit{T}"/>
/// and drained via <see cref="World.CreateEventReader{T}"/>. Unlike <see cref="IComponent"/>/
/// <see cref="ITag"/>, an event is never archetype-resident, query-matchable, or persisted -
/// it's a transient, double-buffered log retained across exactly two ticks. Implement on an
/// empty or data-carrying <c>struct</c>.
/// </summary>
public interface IEvent
{
}
