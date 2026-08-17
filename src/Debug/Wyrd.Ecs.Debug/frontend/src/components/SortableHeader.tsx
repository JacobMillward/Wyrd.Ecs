import { css, cx } from '@linaria/core';
import { Icon } from './Icon';
import { ChevronUp, ChevronDown } from '../icons';

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
    cursor: pointer;
    user-select: none;
    white-space: nowrap;
`;

const active = css`
    color: var(--wyrd-accent-high);
    opacity: 1;
`;

export interface SortableHeaderProps {
    label: string;
    active?: boolean;
    descending: boolean;
    onClick: () => void;
}

// Click toggles direction on the currently-sorted column, or (via the caller's onClick
// handler, which owns the actual column-tracking state) switches to a different column
// ascending: same logic the old Entity Browser used.
export function SortableHeader({ label, active: isActive, descending, onClick }: SortableHeaderProps) {
    return (
        <th class={cx(th, isActive && active)} onClick={onClick}>
            {label} {isActive && <Icon svg={descending ? ChevronDown : ChevronUp} />}
        </th>
    );
}
