import { css, cx } from '@linaria/core';

const chip = css`
    display: inline-block;
    padding: 1px 7px;
    border-radius: 10px;
    font-size: 10.5px;
    margin-right: 4px;
    background: var(--wyrd-chip-bg);
    color: var(--wyrd-chip-text);
    border: 1px solid var(--wyrd-chip-border);
`;

const tag = css`
    background: var(--wyrd-accent-low);
    color: var(--wyrd-accent-high);
    border-color: var(--wyrd-accent);
`;

export interface ChipProps {
    text: string;
    isTag?: boolean;
}

export function Chip({ text, isTag }: ChipProps) {
    return <span class={cx(chip, isTag && tag)}>{text}</span>;
}
