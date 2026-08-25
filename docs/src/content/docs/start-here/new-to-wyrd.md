---
title: New to Wyrd
description: What an ECS is, and where to start if you've never used one.
---

Wyrd is a game engine built around an ECS, entity-component-system. Instead of giving each game object its own class with its own behavior, an ECS splits a game into three separate pieces: entities (just IDs), components (plain data attached to an entity, like a position or a health value), and systems (code that runs against every entity with a particular set of components). A goblin isn't a `Goblin` class, it's an ID with a `Health`, a `Position`, and whatever else makes it a goblin that tick.

The payoff: behavior lives in systems, not in inheritance trees. A `MovementSystem` that reads `Position` and `Velocity` runs against every entity that has both, a player, a goblin, a thrown rock, without any of them knowing the others exist.

## Where to start

[Wyrd in 10 minutes](/start-here/wyrd-in-10-minutes/) is the fastest way in: a component, a system, and the entities it acts on, running in a few minutes. From there, [Build with Wyrd](/build/ecs/entities-and-components/) covers each part of the engine task by task, and [Understand Wyrd](/understand/ecs-architecture/) explains the design decisions once you've used the pieces.

:::tip
Already comfortable with ECS from another engine or library? [Already know ECS?](/start-here/already-know-ecs/) skips the introduction and gets straight to what's different about Wyrd.
:::

## Next

[Wyrd in 10 minutes](/start-here/wyrd-in-10-minutes/) walks through an actual working example.
