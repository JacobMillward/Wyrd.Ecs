---
title: Remapping
description: Saving and loading player-customized key bindings.
---

A `BindingTable<TAction>` built in code is the default. `SaveOverrides`/`LoadOverrides` add a persistence layer on top, for a rebinding UI that needs to survive a relaunch.

## Saving

```csharp
bindings.SaveOverrides("bindings.json");
```

Writes every current binding as human-editable JSON, keyed by profile and action name. Device assignments (`AssignDevice`, see [Multi-Device](/advanced/input/multi-device/)) are deliberately not saved, SDL device ids aren't stable across a relaunch, saving one would just be wrong the next time the game starts.

## Loading

```csharp
bindings.LoadOverrides("bindings.json");
```

Call this once, right after building the table with your code defaults, before registering it with `AddInput`. For each action the file actually contains, it replaces (not adds to) that action's bindings. A missing file is a no-op, code defaults stay in place, which is also what a first launch with no save yet looks like.

:::note
Both methods also take an `IPersistenceStore` overload (`SaveOverrides(IPersistenceStore)`/`LoadOverrides(IPersistenceStore)`) instead of a bare path, for wiring into whatever storage strategy the rest of your save data uses. See [Persistence](/guides/persistence/) for `IPersistenceStore`.
:::

## Next

[Input](/engine/input/) covers the binding and reading side these files persist.
