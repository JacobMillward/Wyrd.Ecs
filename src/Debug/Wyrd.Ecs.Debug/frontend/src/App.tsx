import { render } from 'preact';
import { useEffect, useState } from 'preact/hooks';
import { css } from '@linaria/core';
import type { WorldSnapshot } from './generated/world-snapshot';
import type { ChangeLogEntry } from './generated/change-log-entry';

const heading = css`
  color: #b8860b;
  font-family: sans-serif;
`;

const pre = css`
  background: #1a1a1a;
  color: #ddd;
  padding: 1rem;
  overflow: auto;
  max-height: 40vh;
`;

function App() {
    const [snapshot, setSnapshot] = useState<WorldSnapshot | null>(null);
    const [changelog, setChangelog] = useState<ChangeLogEntry[]>([]);

    useEffect(() => {
        fetch('/api/snapshot')
            .then((r) => (r.ok ? r.json() : null))
            .then(setSnapshot);
        fetch('/api/changelog')
            .then((r) => r.json())
            .then(setChangelog);

        const events = new EventSource('/api/events');
        events.addEventListener('snapshot', (e) => setSnapshot(JSON.parse((e as MessageEvent).data)));
        events.addEventListener('changelog', (e) => setChangelog(JSON.parse((e as MessageEvent).data)));
        return () => events.close();
    }, []);

    return (
        <div>
            <h1 class={heading}>Wyrd.Ecs Debug</h1>
            <h2>Snapshot</h2>
            <pre class={pre}>{JSON.stringify(snapshot, null, 2)}</pre>
            <h2>Change Log</h2>
            <pre class={pre}>{JSON.stringify(changelog, null, 2)}</pre>
        </div>
    );
}

render(<App />, document.getElementById('app')!);
