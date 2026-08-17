import { useEffect } from 'preact/hooks';
import type { RefObject } from 'preact';

// Closes a popover when a click lands anywhere outside `anchorRef`'s element: capture
// phase so this fires before a different popover's own trigger click handler does.
// Opening one popover while another is open closes the first as a side effect of this
// same check (the second trigger click counts as "outside" the first popover's
// anchor), so "only one open at a time" needs no separate coordination.
export function useOutsideClick(anchorRef: RefObject<HTMLElement>, active: boolean, onOutsideClick: () => void): void {
    useEffect(() => {
        if (!active) return;

        const handler = (event: MouseEvent) => {
            if (anchorRef.current && !anchorRef.current.contains(event.target as Node)) {
                onOutsideClick();
            }
        };
        document.addEventListener('click', handler, true);
        return () => document.removeEventListener('click', handler, true);
    }, [active, onOutsideClick]);
}
