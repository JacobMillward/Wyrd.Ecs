import { render } from 'preact';
import { css } from '@linaria/core';

const heading = css`
  color: #b8860b;
  font-family: sans-serif;
`;

function App() {
    return <h1 class={heading}>Wyrd.Ecs Debug</h1>;
}

render(<App />, document.getElementById('app')!);
