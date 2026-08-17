import type { Entity } from './generated/entity';

// Same "#id.generation" format the old UI's EntityDisplay.Format used: plain
// formatting, not a design decision, so it carries forward unchanged.
export function formatEntity(entity: Entity): string {
    return `#${entity.id}.${entity.generation}`;
}

export function entityEquals(a: Entity | null, b: Entity): boolean {
    return a !== null && a.id === b.id && a.generation === b.generation;
}
