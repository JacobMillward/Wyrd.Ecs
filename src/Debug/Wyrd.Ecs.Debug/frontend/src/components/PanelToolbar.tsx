import type { ComponentChildren } from 'preact';
import { css } from '@linaria/core';

const toolbar = css`
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 6px 12px;
    font-size: 12px;
    border-bottom: 1px solid var(--wyrd-hairline);
    background: var(--wyrd-bg);
`;

const count = css`
    margin-left: auto;
    opacity: 0.55;
    font-size: 11px;
    color: var(--wyrd-text);
    white-space: nowrap;
`;

export interface PanelToolbarProps {
    countText?: string;
    children?: ComponentChildren;
}

export function PanelToolbar({ countText, children }: PanelToolbarProps) {
    return (
        <div class={toolbar}>
            {children}
            {countText && <span class={count}>{countText}</span>}
        </div>
    );
}
