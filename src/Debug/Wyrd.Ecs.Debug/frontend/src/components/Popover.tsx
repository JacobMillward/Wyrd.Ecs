import { useRef, useState } from 'preact/hooks';
import type { ComponentChildren } from 'preact';
import { css, cx } from '@linaria/core';
import { useOutsideClick } from './useOutsideClick';
import { Icon } from './Icon';

const anchor = css`
    position: relative;
    display: inline-block;
`;

const trigger = css`
    display: inline-flex;
    align-items: center;
    gap: 4px;
    width: 26px;
    height: 26px;
    justify-content: center;
    border-radius: 4px;
    cursor: pointer;
    background: var(--wyrd-bg-nav);
    color: var(--wyrd-accent-high);
    border: 1px solid var(--wyrd-hairline);
    font-size: 11px;
    padding: 0 8px;

    &:hover {
        border-color: var(--wyrd-accent);
    }
`;

const triggerWithLabel = css`
    width: auto;
`;

const triggerActive = css`
    background: var(--wyrd-accent-low);
    border-color: var(--wyrd-accent);
`;

const content = css`
    position: absolute;
    top: 100%;
    left: 0;
    margin-top: 4px;
    background: var(--wyrd-bg-nav);
    border: 1px solid var(--wyrd-hairline);
    border-radius: 4px;
    padding: 8px;
    font-size: 12px;
    color: var(--wyrd-text);
    z-index: 10;
    white-space: nowrap;
`;

export interface PopoverProps {
    icon: string;
    label: string;
    showLabel?: boolean;
    active?: boolean;
    title?: string;
    children: ComponentChildren;
}

// Click toggles open/closed; useOutsideClick closes it when a click lands anywhere
// outside the anchor. "Only one open at a time" falls out of that same mechanism:
// opening a second Popover means clicking its trigger, which is "outside" the first
// Popover's anchor, closing it as a side effect.
export function Popover({ icon, label, showLabel, active, title, children }: PopoverProps) {
    const [open, setOpen] = useState(false);
    const anchorRef = useRef<HTMLDivElement>(null);

    useOutsideClick(anchorRef, open, () => setOpen(false));

    return (
        <div class={anchor} ref={anchorRef}>
            <button
                type="button"
                class={cx(trigger, showLabel && triggerWithLabel, active && triggerActive)}
                title={title ?? label}
                aria-pressed={active}
                onClick={() => setOpen((value) => !value)}
            >
                <Icon svg={icon} />
                {showLabel && label}
            </button>
            {open && <div class={content}>{children}</div>}
        </div>
    );
}
