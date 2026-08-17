import { useEffect, useState } from 'preact/hooks';
import type { RefObject } from 'preact';

export type Density = 'compact' | 'comfortable';

// Reports "compact"/"comfortable" whenever ref's own rendered width (or height, if
// dimension: "height") crosses threshold. A plain ResizeObserver-backed hook: unlike
// the old Blazor UI's version, there's no JS-interop boundary to bridge here.
export function useDensity(ref: RefObject<HTMLElement>, threshold: number, dimension: 'width' | 'height' = 'width'): Density {
    const [density, setDensity] = useState<Density>('comfortable');

    useEffect(() => {
        const element = ref.current;
        if (!element) return;

        const observer = new ResizeObserver((entries) => {
            const size = dimension === 'height' ? entries[0].contentRect.height : entries[0].contentRect.width;
            setDensity(size < threshold ? 'compact' : 'comfortable');
        });
        observer.observe(element);
        return () => observer.disconnect();
    }, [ref, threshold, dimension]);

    return density;
}
