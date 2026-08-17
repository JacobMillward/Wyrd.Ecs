import { useEffect, useState } from 'preact/hooks';

export type Density = 'compact' | 'comfortable';

// Returns the observed node as state, not a useRef object: a ref's identity never
// changes even once .current attaches, so an effect keyed on the ref alone never
// re-runs if the ref-bearing element wasn't there yet on the effect's first run (e.g.
// an early-return render before data has loaded). Keying on the node itself makes the
// effect re-run the moment it actually attaches.
export function useDensity(threshold: number, dimension: 'width' | 'height' = 'width'): [Density, (node: HTMLElement | null) => void] {
    const [density, setDensity] = useState<Density>('comfortable');
    const [node, setNode] = useState<HTMLElement | null>(null);

    useEffect(() => {
        if (!node) return;

        const observer = new ResizeObserver((entries) => {
            const size = dimension === 'height' ? entries[0].contentRect.height : entries[0].contentRect.width;
            setDensity(size < threshold ? 'compact' : 'comfortable');
        });
        observer.observe(node);
        return () => observer.disconnect();
    }, [node, threshold, dimension]);

    return [density, setNode];
}
