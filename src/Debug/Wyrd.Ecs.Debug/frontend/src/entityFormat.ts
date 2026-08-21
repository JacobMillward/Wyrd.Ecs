import type { Entity } from './generated/entity';

export function formatEntity(entity: Entity): string {
    return `${entity.id}v${entity.generation}`;
}

export function entityEquals(a: Entity | null, b: Entity): boolean {
    return a !== null && a.id === b.id && a.generation === b.generation;
}
