---
title: Example Guide
description: A guide in my new Starlight docs site.
---

Guides lead a user through a specific task they want to accomplish, often with a sequence of steps.
Writing a good guide requires thinking about what your users are trying to do.

## Further reading

- Read [about how-to guides](https://diataxis.fr/how-to-guides/) in the Diátaxis framework

## Code block theme check

```csharp
public sealed partial class MovementSystem : QuerySystem
{
    protected override IQuery DefineQuery(World world) =>
        world.Query().With<Position, Velocity>();

    // dt scales by Time.Scale automatically; systems don't handle pause themselves
    public void Update(Time time, ref Position position, in Velocity velocity)
    {
        position.X += velocity.X * (float)time.Delta.TotalSeconds;
    }
}
```
