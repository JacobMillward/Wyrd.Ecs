// @ts-check
import { readFileSync } from 'node:fs';
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';
import { ExpressiveCodeTheme } from 'astro-expressive-code';

const wyrdCodeThemeDark = ExpressiveCodeTheme.fromJSONString(
	readFileSync(new URL('./src/styles/wyrd-code-theme.jsonc', import.meta.url), 'utf-8')
);
const wyrdCodeThemeLight = ExpressiveCodeTheme.fromJSONString(
	readFileSync(new URL('./src/styles/wyrd-code-theme-light.jsonc', import.meta.url), 'utf-8')
);

// https://astro.build/config
export default defineConfig({
	site: 'https://wyrd.millward.dev',
	redirects: {
		'/guides/entities-and-components': '/build/ecs/entities-and-components',
		'/guides/queries': '/build/ecs/queries',
		'/guides/relations': '/build/ecs/relations',
		'/guides/relations/parent-child': '/build/ecs/relations/parent-child',
		'/guides/templates': '/build/ecs/templates',
		'/guides/resources': '/build/ecs/resources',
		'/guides/events': '/build/ecs/events',
	},
	integrations: [
		starlight({
			title: 'Wyrd.Ecs',
			logo: { src: './src/assets/wyrd-mark.svg', alt: 'Wyrd.Ecs' },
			pagination: false,
			head: [
				{
					tag: 'link',
					attrs: { rel: 'icon', type: 'image/png', sizes: '16x16', href: '/favicon-16.png' },
				},
				{
					tag: 'link',
					attrs: { rel: 'icon', type: 'image/png', sizes: '32x32', href: '/favicon-32.png' },
				},
				{
					tag: 'link',
					attrs: { rel: 'icon', type: 'image/png', sizes: '48x48', href: '/favicon-48.png' },
				},
				{
					tag: 'link',
					attrs: { rel: 'apple-touch-icon', sizes: '180x180', href: '/favicon-180.png' },
				},
			],
			expressiveCode: {
				themes: [wyrdCodeThemeDark, wyrdCodeThemeLight],
			},
			social: [{ icon: 'github', label: 'GitHub', href: 'https://github.com/JacobMillward/Wyrd.Ecs' }],
			sidebar: [
				{ label: 'Getting Started', slug: 'getting-started' },
				{
					label: 'Build with Wyrd',
					items: [
						{
							label: 'ECS',
							items: [
								{ label: 'Entities & Components', slug: 'build/ecs/entities-and-components' },
								{ label: 'Queries', slug: 'build/ecs/queries' },
								{ label: 'Relations', slug: 'build/ecs/relations' },
								{ label: 'Parent/Child', slug: 'build/ecs/relations/parent-child' },
								{ label: 'Templates', slug: 'build/ecs/templates' },
								{ label: 'Resources', slug: 'build/ecs/resources' },
								{ label: 'Events', slug: 'build/ecs/events' },
							],
						},
					],
				},
				{
					label: 'Guides',
					items: [
						{
							label: 'Game Loop',
							items: [
								{ label: 'Systems', slug: 'guides/systems' },
								{ label: 'Command Buffer', slug: 'guides/systems/command-buffer' },
								{ label: 'System Ordering', slug: 'guides/system-ordering' },
								{ label: 'Timestep, Pause & Timescale', slug: 'guides/timestep-pause-timescale' },
							],
						},
						{ label: 'Debugging', slug: 'guides/debugging' },
						{ label: 'Persistence', slug: 'guides/persistence' },
					],
				},
				{
					label: 'Engine',
					items: [
						{ label: 'Platform', slug: 'engine/platform' },
						{ label: 'Renderer', slug: 'engine/renderer' },
						{ label: 'Sprites', slug: 'engine/renderer/sprites' },
						{ label: 'Meshes', slug: 'engine/renderer/meshes' },
						{ label: 'Input', slug: 'engine/input' },
						{ label: 'Audio', slug: 'engine/audio' },
						{ label: 'Assets', slug: 'engine/assets' },
					],
				},
				{
					label: 'Advanced',
					items: [
						{ label: 'Change Tracking', slug: 'advanced/change-tracking' },
						{
							label: 'Input',
							items: [
								{ label: 'Multi-Device', slug: 'advanced/input/multi-device' },
								{ label: 'Remapping', slug: 'advanced/input/remapping' },
							],
						},
						{ label: 'Custom Rendering', slug: 'advanced/custom-rendering' },
					],
				},
			],
		}),
	],
});
