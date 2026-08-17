import { css, cx } from '@linaria/core';
import { colorForComponent } from '../componentColor';

const chip = css`
    display: inline-flex;
    align-items: center;
    padding: 1px 7px;
    border-radius: 10px;
    font-size: 10.5px;
    background: var(--wyrd-chip-bg);
    color: var(--wyrd-chip-text);
    border: 1px solid var(--wyrd-chip-border);
`;

const tag = css`
    background: var(--wyrd-accent-low);
    color: var(--wyrd-accent-high);
    border-color: var(--wyrd-accent);
`;

// Shared by every panel that lays out a cell of chips (component lists, tag lists):
// wraps onto multiple lines instead of overflowing, with even spacing between chips.
export const chipRow = css`
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 4px;
`;

export interface ChipProps {
    text: string;
    isTag?: boolean;
}

// Tags keep one uniform accent style (that consistency is itself the "this is a tag,
// not a component" signal). Component chips are colored per name instead, via inline
// style since Linaria's classes are extracted statically and can't vary per instance.
export function Chip({ text, isTag }: ChipProps) {
    const color = isTag ? undefined : colorForComponent(text);
    return (
        <span class={cx(chip, isTag && tag)} style={color}>
            {text}
        </span>
    );
}
