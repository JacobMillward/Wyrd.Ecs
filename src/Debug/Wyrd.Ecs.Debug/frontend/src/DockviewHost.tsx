import { render } from 'preact';
import { useEffect, useRef, useState } from 'preact/hooks';
import { css } from '@linaria/core';
import { DockviewComponent, type DockviewApi, type IContentRenderer, type ITabRenderer, type CreateComponentOptions } from 'dockview';
import 'dockview/dist/styles/dockview.css';
import './theme.css';
import { wyrdTheme } from './dockviewTheme';
import { playback } from './store';
import { Icon } from './components/Icon';
import { Slider } from './components/Slider';
import { Play, Pause, RotateCcw, Sun, Moon } from './icons';
import { ArchetypeFilterPanel } from './panels/ArchetypeFilterPanel';
import { EntityBrowserPanel } from './panels/EntityBrowserPanel';
import { EntityInspectorPanel } from './panels/EntityInspectorPanel';
import { ChangeLogPanel } from './panels/ChangeLogPanel';

const LAYOUT_STORAGE_KEY = 'wyrd-debug-layout';
const THEME_STORAGE_KEY = 'wyrd-debug-theme';

const PANEL_ARCHETYPE_FILTER = 'archetype-filter';
const PANEL_ENTITY_BROWSER = 'entity-browser';
const PANEL_ENTITY_INSPECTOR = 'entity-inspector';
const PANEL_CHANGE_LOG = 'change-log';

const PANEL_COMPONENTS: Record<string, () => preact.JSX.Element> = {
    [PANEL_ARCHETYPE_FILTER]: ArchetypeFilterPanel,
    [PANEL_ENTITY_BROWSER]: EntityBrowserPanel,
    [PANEL_ENTITY_INSPECTOR]: EntityInspectorPanel,
    [PANEL_CHANGE_LOG]: ChangeLogPanel,
};

const header = css`
    display: flex;
    align-items: center;
    gap: 14px;
    padding: 8px 14px;
    font-size: 13px;
    background: var(--wyrd-bg-nav);
    border-bottom: 1px solid var(--wyrd-hairline);
    color: var(--wyrd-text);
`;

const title = css`
    font-weight: 600;
    margin-right: auto;
`;

const iconButton = css`
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 26px;
    height: 26px;
    border-radius: 4px;
    cursor: pointer;
    background: var(--wyrd-bg-nav);
    color: var(--wyrd-accent-high);
    border: 1px solid var(--wyrd-hairline);

    &:hover {
        border-color: var(--wyrd-accent);
    }
`;

const timescale = css`
    display: flex;
    align-items: center;
    gap: 6px;
    font-size: 11px;
    opacity: 0.85;
`;

const host = css`
    height: calc(100% - 41px);
`;

async function postPlaybackPause(): Promise<void> {
    await fetch('/api/playback/pause', { method: 'POST' });
}

async function postPlaybackResume(): Promise<void> {
    await fetch('/api/playback/resume', { method: 'POST' });
}

async function postPlaybackTimescale(value: number): Promise<void> {
    await fetch('/api/playback/timescale', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ value }),
    });
}

function currentEffectiveTheme(): 'light' | 'dark' {
    const stored = localStorage.getItem(THEME_STORAGE_KEY);
    if (stored === 'light' || stored === 'dark') return stored;
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

// createComponent mounts a fresh, independent Preact tree into the container Dockview
// gives it (one per panel, since dockview creates containers outside any single
// component's control), and unmounts it on dispose: the direct Preact analogue of the
// old code's window.Blazor.rootComponents.add(...)/.dispose().
function createComponent(options: CreateComponentOptions): IContentRenderer {
    const element = document.createElement('div');
    element.style.height = '100%';
    const Panel = PANEL_COMPONENTS[options.name];
    return {
        element,
        init: () => render(<Panel />, element),
        dispose: () => render(null, element),
    };
}

// Dockview's own DefaultTab, minus the close button: the panel set is fixed (no
// "add panel" affordance anywhere in this UI), so a closable tab would be a one-way door.
function createTabComponent(): ITabRenderer {
    const element = document.createElement('div');
    element.className = 'dv-default-tab';
    const content = document.createElement('div');
    content.className = 'dv-default-tab-content';
    element.appendChild(content);
    return {
        element,
        init: (params) => {
            content.textContent = params.title ?? '';
            params.api.onDidTitleChange((event) => {
                content.textContent = event.title ?? '';
            });
        },
    };
}

function buildDefaultLayout(api: DockviewApi) {
    api.clear();
    api.addPanel({ id: PANEL_ARCHETYPE_FILTER, component: PANEL_ARCHETYPE_FILTER, title: 'Archetypes', minimumWidth: 180 });
    api.addPanel({
        id: PANEL_ENTITY_BROWSER,
        component: PANEL_ENTITY_BROWSER,
        title: 'Entities',
        position: { referencePanel: PANEL_ARCHETYPE_FILTER, direction: 'right' },
        minimumWidth: 320,
    });
    api.addPanel({
        id: PANEL_ENTITY_INSPECTOR,
        component: PANEL_ENTITY_INSPECTOR,
        title: 'Inspector',
        position: { referencePanel: PANEL_ENTITY_BROWSER, direction: 'right' },
        minimumWidth: 220,
    });
    api.addPanel({
        id: PANEL_CHANGE_LOG,
        component: PANEL_CHANGE_LOG,
        title: 'Change Log',
        position: { direction: 'below' },
        minimumHeight: 80,
    });
}

function loadOrBuildDefaultLayout(api: DockviewApi) {
    const saved = localStorage.getItem(LAYOUT_STORAGE_KEY);
    if (saved) {
        try {
            api.fromJSON(JSON.parse(saved));
            return;
        } catch {
            // Corrupted/incompatible saved layout: fall through to the shipped default.
        }
    }
    buildDefaultLayout(api);
}

export function DockviewHost() {
    const hostRef = useRef<HTMLDivElement>(null);
    const dockviewRef = useRef<DockviewComponent | null>(null);
    const [theme, setTheme] = useState<'light' | 'dark'>(currentEffectiveTheme);

    useEffect(() => {
        const element = hostRef.current;
        if (!element) return;

        const dockview = new DockviewComponent(element, {
            theme: wyrdTheme,
            createComponent,
            createTabComponent,
        });
        dockviewRef.current = dockview;

        let saveHandle: ReturnType<typeof setTimeout> | undefined;
        const layoutChangeDisposable = dockview.api.onDidLayoutChange(() => {
            if (saveHandle) clearTimeout(saveHandle);
            saveHandle = setTimeout(() => localStorage.setItem(LAYOUT_STORAGE_KEY, JSON.stringify(dockview.api.toJSON())), 300);
        });
        loadOrBuildDefaultLayout(dockview.api);

        return () => {
            layoutChangeDisposable.dispose();
            dockview.dispose();
        };
    }, []);

    function resetLayout() {
        localStorage.removeItem(LAYOUT_STORAGE_KEY);
        if (dockviewRef.current) buildDefaultLayout(dockviewRef.current.api);
    }

    function toggleTheme() {
        const next = theme === 'light' ? 'dark' : 'light';
        document.documentElement.dataset.theme = next;
        localStorage.setItem(THEME_STORAGE_KEY, next);
        setTheme(next);
    }

    const isPaused = playback.value.isPaused;

    return (
        <>
            <header class={header}>
                <span class={title}>Wyrd.Ecs Debug</span>
                <button type="button" class={iconButton} title={isPaused ? 'Resume' : 'Pause'} aria-pressed={isPaused} onClick={() => (isPaused ? postPlaybackResume() : postPlaybackPause())}>
                    <Icon svg={isPaused ? Play : Pause} />
                </button>
                <label class={timescale}>
                    <span>Timescale</span>
                    <Slider value={playback.value.timeScale} min={0} max={4} step={0.1} decimals={1} onCommit={postPlaybackTimescale} />
                </label>
                <button type="button" class={iconButton} title="Reset Layout" onClick={resetLayout}>
                    <Icon svg={RotateCcw} />
                </button>
                <button type="button" class={iconButton} title={theme === 'dark' ? 'Switch to light theme' : 'Switch to dark theme'} onClick={toggleTheme}>
                    <Icon svg={theme === 'dark' ? Sun : Moon} />
                </button>
            </header>
            <div class={host} ref={hostRef}></div>
        </>
    );
}
