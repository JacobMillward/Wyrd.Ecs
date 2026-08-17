import { render } from 'preact';
import { connect } from './store';
import { DockviewHost } from './DockviewHost';

connect();

render(<DockviewHost />, document.getElementById('app')!);
