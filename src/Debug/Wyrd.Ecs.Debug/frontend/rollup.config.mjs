import wyw from '@wyw-in-js/rollup';
import postcss from 'rollup-plugin-postcss';
import typescript from '@rollup/plugin-typescript';
import { nodeResolve } from '@rollup/plugin-node-resolve';

export default {
  input: 'src/App.tsx',
  output: {
    // Rollup rejects emitted asset filenames containing ".." (relative parent
    // traversal), so js/ and css/ are expressed as subpaths of a shared
    // output.dir rather than as output.file + a "../css/..." extract path.
    dir: '../wwwroot',
    entryFileNames: 'js/app.js',
    format: 'esm',
  },
  plugins: [
    wyw({ include: ['**/*.tsx', '**/*.ts'] }),
    postcss({ extract: 'css/app.css' }),
    typescript(),
    nodeResolve(),
  ],
};
