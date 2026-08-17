import { signal } from '@preact/signals';
import type { WorldSnapshot } from './generated/world-snapshot';
import type { ChangeLogEntry } from './generated/change-log-entry';
import type { Entity } from './generated/entity';
import type { PlaybackSnapshot } from './generated/playback-snapshot';

// Every panel mounts as its own, separate Preact tree (dockview creates one container
// per panel, outside any single component's control, see DockviewHost), so Context
// can't cross panel boundaries. These module-level signals are the shared state every
// panel reads instead; @preact/signals re-renders only the components that actually
// read a signal that changed, no manual subscribe/unsubscribe bookkeeping needed.
export const snapshot = signal<WorldSnapshot | null>(null);
export const changelog = signal<ChangeLogEntry[]>([]);
export const selectedEntity = signal<Entity | null>(null);
export const filteredArchetypeKeys = signal<Set<string>>(new Set());
export const playback = signal<PlaybackSnapshot>({ isPaused: false, timeScale: 1 });

export function selectEntity(entity: Entity): void {
    selectedEntity.value = entity;
}

export function toggleArchetypeFilter(key: string): void {
    const next = new Set(filteredArchetypeKeys.value);
    if (!next.delete(key)) next.add(key);
    filteredArchetypeKeys.value = next;
}

export function clearArchetypeFilters(): void {
    filteredArchetypeKeys.value = new Set();
}

// One EventSource for the whole app (opened once from App.tsx), not one per panel:
// four independent SSE connections would be wasteful and would need their own
// coordination. Initial GETs cover the gap between page load and the first SSE push;
// the SSE handlers below then keep every signal live for as long as the tab is open.
export function connect(): () => void {
    fetch('/api/snapshot')
        .then((r) => (r.ok ? (r.json() as Promise<WorldSnapshot>) : null))
        .then((value) => {
            if (value) snapshot.value = value;
        });
    fetch('/api/changelog')
        .then((r) => r.json() as Promise<ChangeLogEntry[]>)
        .then((value) => (changelog.value = value));
    fetch('/api/playback')
        .then((r) => r.json() as Promise<PlaybackSnapshot>)
        .then((value) => (playback.value = value));

    const events = new EventSource('/api/events');
    events.addEventListener('snapshot', (e) => (snapshot.value = JSON.parse((e as MessageEvent).data)));
    events.addEventListener('changelog', (e) => (changelog.value = JSON.parse((e as MessageEvent).data)));
    events.addEventListener('playback', (e) => (playback.value = JSON.parse((e as MessageEvent).data)));

    return () => events.close();
}
