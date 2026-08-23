# Wyrd.Ecs docs

The documentation site published at [wyrd.millward.dev](https://wyrd.millward.dev), built with [Astro](https://astro.build) + [Starlight](https://starlight.astro.build).

## Working on it

```
npm install
npm run dev      # local preview at localhost:4321
npm run build    # static build to dist/
```

Pages live in `src/content/docs/`, one `.md`/`.mdx` per route. The sidebar is defined by hand in `astro.config.mjs` - adding a page means adding its entry there too. Guides end with a `## Next` transition pointing onward (backward where a page closes out its section).

Deploy happens automatically via GitHub Actions (`.github/workflows/docs.yml`) when `docs/**` changes land on `main`.
