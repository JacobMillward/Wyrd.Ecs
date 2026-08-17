import type { DockviewTheme } from 'dockview';

// colorScheme is left undefined deliberately - this theme's actual colors follow
// theme.css's light/dark cascade via CSS custom properties, not a fixed scheme.
export const wyrdTheme: DockviewTheme = {
    name: 'wyrd',
    className: 'wyrd-dockview-theme',
};
