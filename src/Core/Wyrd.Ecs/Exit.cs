namespace Wyrd.Ecs;

/// <summary>
/// Signals <c>World.Run</c> to stop. Emitted by <see cref="World.RequestExit"/>, or
/// directly via <see cref="World.Emit{T}"/> by anything with a reason to end the app - a
/// pause menu's "Quit" button, a game-over system, a dedicated server's watchdog. <see cref="Code"/>
/// defaults to <c>0</c> (clean shutdown); a nonzero value signals failure, mirroring Bevy's
/// <c>AppExit::Success</c>/<c>AppExit::Error</c>. An ordinary event otherwise - nothing about
/// it is special-cased by <see cref="World.Emit{T}"/> or <see cref="World.CreateEventReader{T}"/>,
/// only <c>World.Run</c> gives it meaning, by reading it.
/// </summary>
public readonly record struct Exit(int Code = 0) : IEvent;
