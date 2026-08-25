---
title: Relations
description: How a relation edge is actually stored, and why one entity can carry any number of them for one archetype bit.
---

[Relations](/build/ecs/relations/) covers declaring and querying them. Underneath, an edge isn't a separate kind of storage, it's an ordinary component wearing a different shape.

## An edge is one component, however many targets

Adding a relation edge doesn't add a new archetype-level entry per edge. Every entity's outgoing edges for one relation type live in a single component, `RelationLinks<T>`, holding all of that entity's targets together. An entity targeting five others at once and one targeting a single entity both occupy the exact same archetype bit, adding a tenth target changes the component's contents, not the entity's shape. That's also the whole story behind [`WithRelation<T>()`](/build/ecs/queries/#filtering-by-relation) being sugar for `With<RelationLinks<T>>()`, it's filtering on a real component like any other.

## Reverse lookups get their own storage

Finding every entity with an edge pointing at a given target isn't something `RelationLinks<T>` can answer without scanning every entity that might have one. A separate component, `RelationBacklinks<T>`, exists to make that lookup direct instead, maintained alongside the forward edges so cascading destruction (see [Parent/Child](/build/ecs/relations/parent-child/)) can find every dependent entity without a linear scan.

## Next

[Change Tracking](/understand/change-tracking/) covers watching an entity's relation edges change over time, not just reading their current state.
