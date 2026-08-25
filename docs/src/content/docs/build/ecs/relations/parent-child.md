---
title: Parent/Child
description: The built-in scene-hierarchy relation, and the helpers for walking it.
---

`Parent` is a relation like any other, not a special case wired into the engine. It just happens to be the one Wyrd ships for you:

```csharp
public readonly struct Parent : IRelation, IExclusiveRelation, IDependent { }
```

Exclusive because a child has one parent at a time. Dependent because destroying a parent should take its subtree with it.

## Setting and clearing

```csharp
child.SetParent(parent);
child.ClearParent();
```

`SetParent`/`ClearParent` are `EntityView` mutators, chainable like any other. To reparent, call `SetParent` alone, a preceding `ClearParent` costs an extra archetype move for nothing.

From the parent's side, the same edge, called from whichever entity you have in hand:

```csharp
parent.AddChild(child);
parent.RemoveChild(child);
```

## Walking the tree

```csharp
world.Children(parent);       // direct children
world.Ancestors(entity);      // parent chain, closest first
world.Descendants(entity);    // every descendant, depth-first
world.TryGetParent(child, out var parent);
```

:::caution
None of these guard against a cycle. Assigning a descendant as its own ancestor's parent loops forever, same as any other caller-error case Wyrd doesn't defensively validate against.
:::

## Cascading destruction

Destroying a parent destroys its whole subtree, not just the parent. This comes from `Parent` implementing `IDependent`, the same mechanism [Relations](/build/ecs/relations/#cascading-destruction) covers for any relation, not a hierarchy-specific special case.

## Next

Relations link existing entities together. For starting several entities from one reusable definition, see [Templates](/build/ecs/templates/).
