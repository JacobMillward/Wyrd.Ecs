---
title: Events
description: One-off signals for decoupling systems, without either one knowing about the other.
---

A `CombatSystem` deals damage and needs to tell an `AudioSystem`, a floating-damage-number UI, and an achievement tracker, without knowing any of them exist. That's what events are for: emit once, let anyone interested read it.

## Emitting

```csharp
public struct DamageDealt : IEvent
{
    public Entity Target;
    public int Amount;
}

world.Emit(new DamageDealt { Target = goblin, Amount = 5 });
```

`IEvent` is a marker interface, same shape as `IComponent`. `Emit` is immediate, no `CommandBuffer` involved, and safe to call concurrently from several systems in the same parallel stage.

## Reading

```csharp
public sealed partial class DamageNumbersSystem(World world) : QuerySystem
{
    private readonly EventReader<DamageDealt> _damageEvents = world.CreateEventReader<DamageDealt>();

    protected override IQuery DefineQuery(Query query) => query.With<Position>();

    public void Update(Time time)
    {
        foreach (var damage in _damageEvents.Read())
        {
            // spawn a floating number at damage.Target's position
        }
    }
}
```

Create one `EventReader<T>` per system that cares, store it, call `Read()` once every tick. Each reader tracks its own position independently, one system reading `DamageDealt` doesn't affect what another sees.

:::caution
An event stays readable for the `Update` call it was emitted in, plus the one after that, then it's gone. A reader that calls `Read()` less often than every `Update` can miss whatever fell outside that window.
:::

:::tip
Need to know what a component's value actually became, not just that something happened, or need it to persist across more than two ticks? See [Change Tracking](/understand/change-tracking/).
:::

## Next

Events tell you something happened. For inspecting live world state directly, see [Debugging](/build/debugging/).
