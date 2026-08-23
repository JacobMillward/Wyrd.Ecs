---
title: Templates
description: Reusable definitions for an entity's starting components, tags, and children.
---

Spawning a level's worth of goblins means building the same `Health`/`Enemy` shape over and over. `EntityTemplate` defines it once, then each spawn instantiates it.

## Defining a template

```csharp
var goblinTemplate = new EntityTemplate()
    .AddComponent(new Health { Current = 10, Max = 10 })
    .AddTag<Enemy>();
```

Or subclass it for a named, hand-authored prefab instead of building one at runtime:

```csharp
public sealed class GoblinTemplate : EntityTemplate
{
    public GoblinTemplate()
    {
        AddComponent(new Health { Current = 10, Max = 10 });
        AddTag<Enemy>();
    }
}
```

## Instantiating it

```csharp
world.Commands.CreateEntity(goblinTemplate);
```

Returns the same chainable `EntityView` a plain `CreateEntity()` does, so you can keep adding to this one instance on top of what the template already set:

```csharp
Entity eliteGoblin = world.Commands.CreateEntity(goblinTemplate)
    .AddTag<Elite>();
```

For many at once:

```csharp
Entity[] goblins = world.Commands.CreateEntity(goblinTemplate, 20);
```

## Child subtrees

```csharp
var swordTemplate = new EntityTemplate().AddComponent(new Weapon { Damage = 5 });
goblinTemplate.AddChild(swordTemplate);
```

Instantiating `goblinTemplate` now instantiates the sword too, connected via the `Parent` relation, in one archetype move per node. `swordTemplate` is reusable from other parents as well, each instantiation gets its own independent entities.

:::note
Batch instantiation (`CreateEntity(template, count)`) doesn't support templates with children, each child is a distinct set of entities per instance. Call `CreateEntity(template)` once per instance instead.
:::

## Frozen after first use

A template is mutable until the moment it's first instantiated, then further `AddComponent`/`AddTag`/`AddChild` calls throw. Build the whole shape before handing it to `CreateEntity` the first time.

## Next

Templates and relations both shape entities you already know about ahead of time. For state that belongs to no entity at all, see [Resources](/guides/resources/).
