import { useRef, useState } from 'preact/hooks';
import { css } from '@linaria/core';
import { snapshot, filteredArchetypeKeys, toggleArchetypeFilter } from '../store';
import { archetypeKey } from '../archetypeKey';
import { useDensity } from '../components/useDensity';
import { PanelToolbar } from '../components/PanelToolbar';
import { SortableHeader } from '../components/SortableHeader';
import { Chip, chipRow } from '../components/Chip';
import { Icon } from '../components/Icon';
import { Filter } from '../icons';
import type { ArchetypeSnapshot } from '../generated/archetype-snapshot';

const content = css`
    height: 100%;
    overflow: auto;
`;

const table = css`
    width: 100%;
    border-collapse: collapse;
    font-size: 12px;
`;

const td = css`
    padding: 6px 10px;
    border-bottom: 1px solid var(--wyrd-hairline);
    color: var(--wyrd-text);
`;

const filterButton = css`
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 22px;
    height: 22px;
    border-radius: 4px;
    cursor: pointer;
    background: var(--wyrd-bg-nav);
    color: var(--wyrd-accent-high);
    border: 1px solid var(--wyrd-hairline);
`;

const filterButtonActive = css`
    background: var(--wyrd-accent-low);
    border-color: var(--wyrd-accent);
`;

const emptyState = css`
    padding: 16px;
    opacity: 0.6;
    font-size: 12px;
`;

const noComponents = css`
    opacity: 0.6;
`;

function compositionTitle(archetype: ArchetypeSnapshot): string {
    return archetype.componentDiscriminators.length > 0 ? archetype.componentDiscriminators.join(', ') : '(no components)';
}

export function ArchetypeFilterPanel() {
    const [sortDescending, setSortDescending] = useState(true);
    const rootRef = useRef<HTMLDivElement>(null);
    const density = useDensity(rootRef, 260);

    const archetypes = snapshot.value?.archetypes ?? null;

    if (archetypes === null) {
        return <p class={emptyState}>Waiting for snapshot&hellip;</p>;
    }

    const totalEntities = archetypes.reduce((sum, a) => sum + a.entityCount, 0);
    const sorted = [...archetypes].sort((a, b) => (sortDescending ? b.entityCount - a.entityCount : a.entityCount - b.entityCount));

    return (
        <>
            <PanelToolbar countText={`${archetypes.length} archetype${archetypes.length === 1 ? '' : 's'} · ${totalEntities} entities`} />
            <div class={content} ref={rootRef}>
                {archetypes.length === 0 ? (
                    <p class={emptyState}>No archetypes.</p>
                ) : (
                    <table class={table}>
                        <thead>
                            <tr>
                                <th class={td}>Composition</th>
                                <SortableHeader label="Entities" active descending={sortDescending} onClick={() => setSortDescending((value) => !value)} />
                                <th class={td}></th>
                            </tr>
                        </thead>
                        <tbody>
                            {sorted.map((archetype) => {
                                const key = archetypeKey(archetype);
                                const active = filteredArchetypeKeys.value.has(key);
                                return (
                                    <tr key={key}>
                                        <td class={td} title={compositionTitle(archetype)}>
                                            <div class={chipRow}>
                                                {archetype.componentDiscriminators.length === 0 ? (
                                                    <span class={noComponents}>(no components)</span>
                                                ) : (
                                                    archetype.componentDiscriminators.map((c) => <Chip key={c} text={c} />)
                                                )}
                                                {density === 'comfortable' && archetype.tagDiscriminators.map((tag) => <Chip key={tag} text={tag} isTag />)}
                                            </div>
                                        </td>
                                        <td class={td}>{archetype.entityCount}</td>
                                        <td class={td}>
                                            <button
                                                type="button"
                                                class={active ? `${filterButton} ${filterButtonActive}` : filterButton}
                                                title={active ? 'Remove from entity filter' : 'Filter entity list to this archetype'}
                                                aria-pressed={active}
                                                onClick={() => toggleArchetypeFilter(key)}
                                            >
                                                <Icon svg={Filter} />
                                            </button>
                                        </td>
                                    </tr>
                                );
                            })}
                        </tbody>
                    </table>
                )}
            </div>
        </>
    );
}
