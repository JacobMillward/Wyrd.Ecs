# Asteroids

A small, complete Asteroids clone built on Wyrd.Ecs — the engine's flagship example.
It's not a tech demo of one feature; it's a real (if tiny) game, because that's the only
way to show what building on Wyrd actually feels like end to end.

## Run it

```
dotnet run --project examples/Asteroids
```

Add `--debug` to also serve the live debug UI at `http://127.0.0.1:5299`.

## Controls

| Key | Action |
|---|---|
| W / Up | Thrust |
| A/D or Left/Right | Turn |
| Space | Fire |
| P | Pause/resume |
| S | Save |
| L | Load |
| R | Reset (respawn ship and asteroids, zero the score) |

## What it demonstrates

- Archetype storage, `QuerySystem` + source-generated dispatch, `EntityTemplate` prefabs
  (`Systems/`, ship/bullet/asteroid templates in `Program.cs`).
- Fixed timestep, pause, and timescale (`[FixedTimestep]` throughout; `P`; the brief
  slow-mo on ship death in `GameOverSystem`).
- Relations/hierarchy: the ship's engine flame is a child entity, positioned and rotated
  by the parent's `Transform` automatically, its visibility toggled via tint alpha on a
  transparent-blended `Material`.
- Events: `AsteroidDestroyed`/`ShipDestroyed` decouple scoring, audio, and splitting
  from the collision system that detects them.
- Rendering, input, and audio: sprites through an orthographic camera, strongly-typed
  input actions, mixing buses for the engine loop vs. one-shot SFX.
- Persistence: `S`/`L` save and load the entire run as human-readable JSON.
- The debug UI, entirely opt-in behind `--debug`.
- A real-time main loop: `world.Update(...)` is driven by an actual measured
  `Stopwatch` delta each iteration, not a hardcoded per-frame constant — without this,
  a loop with nothing throttling its iteration rate runs far faster than real time.

## Known limitation

Score and game-over state show in the window title bar, not on-screen text —
`Wyrd.Ecs.Renderer` has no font/glyph support yet.
