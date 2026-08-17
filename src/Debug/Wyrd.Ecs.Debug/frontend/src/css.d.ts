// rollup-plugin-postcss handles these imports at build time (extracting into
// css/app.css); tsc just needs to know a bare CSS import is a valid module.
declare module '*.css';
