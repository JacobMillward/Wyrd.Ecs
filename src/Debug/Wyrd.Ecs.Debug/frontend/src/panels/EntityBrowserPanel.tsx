import { useRef, useState } from 'preact/hooks';
import { css } from '@linaria/core';
import { snapshot, filteredArchetypeKeys, selectedEntity, selectEntity, toggleArchetypeFilter, clearArchetypeFilters } from '../store';
import { archetypeKey } from '../archetypeKey';
import { formatEntity, entityEquals } from '../entityFormat';
import { useDensity } from '../components/useDensity';
import { PanelToolbar } from '../components/PanelToolbar';
import { SortableHeader } from '../components/SortableHeader';
import { Chip, chipRow } from '../components/Chip';
import { Popover } from '../components/Popover';
import { Columns3, Filter } from '../icons';
import type { InspectedEntity } from '../generated/inspected-entity';

type SortColumn = 'entity' | 'components' | 'tags';

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
    font-family: ui-monospace, 'JetBrains Mono', Menlo, monospace;
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

const popoverRow = css`
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    padding: 2px 0;
`;

const linkButton = css`
    background: none;
    border: none;
    color: var(--wyrd-accent-high);
    cursor: pointer;
    font-size: 11px;
    padding: 0;
`;

const emptyState = css`
    padding: 16px;
    opacity: 0.6;
    font-size: 12px;
`;

function matchesSearch(entity: InspectedEntity, needle: string): boolean {
    if (!needle) return true;
    const lower = needle.toLowerCase();
    return (
        formatEntity(entity.entity).toLowerCase().includes(lower) ||
        entity.components.some((c) => c.component.discriminator.toLowerCase().includes(lower)) ||
        entity.tags.some((t) => t.toLowerCase().includes(lower))
    );
}

export function EntityBrowserPanel() {
    const [searchText, setSearchText] = useState('');
    const [showComponents, setShowComponents] = useState(true);
    const [showTags, setShowTags] = useState(true);
    const [sortColumn, setSortColumn] = useState<SortColumn>('entity');
    const [sortDescending, setSortDescending] = useState(false);
    const rootRef = useRef<HTMLDivElement>(null);
    const density = useDensity(rootRef, 640);

    const worldSnapshot = snapshot.value;
    if (worldSnapshot === null) {
        return <p class={emptyState}>Waiting for snapshot&hellip;</p>;
    }

    const filters = filteredArchetypeKeys.value;
    const visible = worldSnapshot.entities.filter(
        (e) => (filters.size === 0 || filters.has(archetypeKey({ componentDiscriminators: e.components.map((c) => c.component.discriminator), tagDiscriminators: e.tags }))) && matchesSearch(e, searchText),
    );

    const sorted = [...visible].sort((a, b) => {
        const diff = sortColumn === 'components' ? a.components.length - b.components.length : sortColumn === 'tags' ? a.tags.length - b.tags.length : a.entity.id - b.entity.id;
        return sortDescending ? -diff : diff;
    });

    function sortBy(column: SortColumn) {
        if (sortColumn === column) setSortDescending((value) => !value);
        else {
            setSortColumn(column);
            setSortDescending(false);
        }
    }

    const activeFilterArchetypes = worldSnapshot.archetypes.filter((a) => filters.has(archetypeKey(a)));

    return (
        <>
            <PanelToolbar countText={`${visible.length} of ${worldSnapshot.entities.length}`}>
                <input class={search} placeholder="Search entities…" value={searchText} onInput={(e) => setSearchText((e.target as HTMLInputElement).value)} />
                <Popover icon={Columns3} label="Columns" title="Columns">
                    <label class={popoverRow}>
                        <span>Components</span>
                        <input type="checkbox" checked={showComponents} onChange={(e) => setShowComponents((e.target as HTMLInputElement).checked)} />
                    </label>
                    <label class={popoverRow}>
                        <span>Tags</span>
                        <input type="checkbox" checked={showTags} onChange={(e) => setShowTags((e.target as HTMLInputElement).checked)} />
                    </label>
                </Popover>
                {filters.size > 0 && (
                    <Popover icon={Filter} label={`${filters.size} archetype${filters.size === 1 ? '' : 's'} filtered`} showLabel active>
                        {activeFilterArchetypes.map((a) => {
                            const key = archetypeKey(a);
                            return (
                                <div key={key} class={popoverRow}>
                                    <span>{a.componentDiscriminators.join(', ') || '(no components)'}</span>
                                    <button type="button" class={linkButton} onClick={() => toggleArchetypeFilter(key)}>
                                        remove
                                    </button>
                                </div>
                            );
                        })}
                        <button type="button" class={linkButton} onClick={clearArchetypeFilters}>
                            Clear all
                        </button>
                    </Popover>
                )}
            </PanelToolbar>
            <div class={content} ref={rootRef}>
                {visible.length === 0 ? (
                    <p class={emptyState}>No entities match the current filter/search.</p>
                ) : (
                    <table class={table}>
                        <thead>
                            <tr>
                                <SortableHeader label="Entity" active={sortColumn === 'entity'} descending={sortDescending} onClick={() => sortBy('entity')} />
                                {showComponents && <SortableHeader label="Components" active={sortColumn === 'components'} descending={sortDescending} onClick={() => sortBy('components')} />}
                                {showTags && density === 'comfortable' && <SortableHeader label="Tags" active={sortColumn === 'tags'} descending={sortDescending} onClick={() => sortBy('tags')} />}
                            </tr>
                        </thead>
                        <tbody>
                            {sorted.map((entity) => (
                                <tr key={formatEntity(entity.entity)} class={entityEquals(selectedEntity.value, entity.entity) ? `${row} ${rowSelected}` : row} onClick={() => selectEntity(entity.entity)}>
                                    <td class={td}>{formatEntity(entity.entity)}</td>
                                    {showComponents && (
                                        <td class={td}>
                                            <div class={chipRow}>
                                                {entity.components.map((c) => (
                                                    <Chip key={c.component.discriminator} text={c.component.discriminator} />
                                                ))}
                                            </div>
                                        </td>
                                    )}
                                    {showTags && density === 'comfortable' && (
                                        <td class={td}>
                                            <div class={chipRow}>
                                                {entity.tags.map((tag) => (
                                                    <Chip key={tag} text={tag} isTag />
                                                ))}
                                            </div>
                                        </td>
                                    )}
                                </tr>
                            ))}
                        </tbody>
                    </table>
                )}
            </div>
        </>
    );
}
