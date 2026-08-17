import { useState } from 'preact/hooks';
import { css } from '@linaria/core';
import { changelog, selectedEntity, selectEntity } from '../store';
import { formatEntity, entityEquals } from '../entityFormat';
import { PanelToolbar } from '../components/PanelToolbar';
import { Chip } from '../components/Chip';
import { ChangeKind } from '../generated/change-kind';
import type { ChangeLogEntry } from '../generated/change-log-entry';

const content = css`
    height: 100%;
    overflow: auto;
`;

const table = css`
    width: 100%;
    border-collapse: collapse;
    font-size: 12px;
`;

// Dense by default, fixed row padding: deliberately no useDensity/ResizeObserver here.
// A log view should read densely at any panel size, not have its padding shift under
// the reader.
const td = css`
    padding: 3px 10px;
    border-bottom: 1px solid var(--wyrd-hairline);
    color: var(--wyrd-text);
`;

const th = css`
    text-align: left;
    font-weight: 500;
    padding: 5px 10px;
    font-size: 10.5px;
    text-transform: uppercase;
    letter-spacing: 0.4px;
    border-bottom: 1px solid var(--wyrd-hairline);
    color: var(--wyrd-text);
    opacity: 0.75;
`;

const row = css`
    cursor: pointer;

    &:hover {
        background: var(--wyrd-bg-nav);
    }
`;

const rowSelected = css`
    background: var(--wyrd-accent-low);
    border-left: 2px solid var(--wyrd-accent);
`;

const addition = css`
    color: var(--wyrd-green);
`;

const removal = css`
    color: var(--wyrd-red);
`;

const search = css`
    border-radius: 4px;
    padding: 4px 8px;
    font-size: 12px;
    width: 150px;
    border: 1px solid var(--wyrd-hairline);
    background: var(--wyrd-bg);
    color: var(--wyrd-text);

    &:focus {
        outline: 2px solid var(--wyrd-accent);
        outline-offset: -1px;
    }
`;

const emptyState = css`
    padding: 16px;
    opacity: 0.6;
    font-size: 12px;
`;

function isAddition(kind: ChangeKind): boolean {
    return kind === ChangeKind.EntityCreated || kind === ChangeKind.ComponentAdded || kind === ChangeKind.TagAdded;
}

function isTagChange(kind: ChangeKind): boolean {
    return kind === ChangeKind.TagAdded || kind === ChangeKind.TagRemoved;
}

function description(kind: ChangeKind): string {
    switch (kind) {
        case ChangeKind.EntityCreated:
            return 'Entity created';
        case ChangeKind.EntityDestroyed:
            return 'Entity destroyed';
        case ChangeKind.ComponentAdded:
            return 'Component added';
        case ChangeKind.ComponentRemoved:
            return 'Component removed';
        case ChangeKind.TagAdded:
            return 'Tag added';
        case ChangeKind.TagRemoved:
            return 'Tag removed';
    }
}

function entryKey(entry: ChangeLogEntry): string {
    return `${entry.tick}-${formatEntity(entry.entity)}-${entry.kind}-${entry.componentName ?? ''}`;
}

function matchesSearch(entry: ChangeLogEntry, needle: string): boolean {
    if (!needle) return true;
    const lower = needle.toLowerCase();
    return (
        formatEntity(entry.entity).toLowerCase().includes(lower) ||
        description(entry.kind).toLowerCase().includes(lower) ||
        (entry.componentName?.toLowerCase().includes(lower) ?? false)
    );
}

export function ChangeLogPanel() {
    const [searchText, setSearchText] = useState('');

    const visible = changelog.value.filter((entry) => matchesSearch(entry, searchText));

    return (
        <>
            <PanelToolbar countText={`${visible.length} of ${changelog.value.length}`}>
                <input class={search} placeholder="Search changes…" value={searchText} onInput={(e) => setSearchText((e.target as HTMLInputElement).value)} />
            </PanelToolbar>
            <div class={content}>
                {visible.length === 0 ? (
                    <p class={emptyState}>No changes match the current search.</p>
                ) : (
                    <table class={table}>
                        <thead>
                            <tr>
                                <th class={th}>Kind</th>
                                <th class={th}>Tick</th>
                                <th class={th}>Entity</th>
                                <th class={th}>Component</th>
                            </tr>
                        </thead>
                        <tbody>
                            {visible.map((entry) => (
                                <tr
                                    key={entryKey(entry)}
                                    class={entityEquals(selectedEntity.value, entry.entity) ? `${row} ${rowSelected}` : row}
                                    onClick={() => selectEntity(entry.entity)}
                                >
                                    <td class={`${td} ${isAddition(entry.kind) ? addition : removal}`}>
                                        <span aria-hidden="true">{isAddition(entry.kind) ? '+' : '−'}</span> {description(entry.kind)}
                                    </td>
                                    <td class={td}>{entry.tick}</td>
                                    <td class={td}>{formatEntity(entry.entity)}</td>
                                    <td class={td}>{entry.componentName && <Chip text={entry.componentName} isTag={isTagChange(entry.kind)} />}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                )}
            </div>
        </>
    );
}
