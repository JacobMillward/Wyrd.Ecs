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
		'/guides/systems': '/build/game-loop/systems',
		'/guides/systems/command-buffer': '/build/game-loop/systems/command-buffer',
		'/guides/system-ordering': '/build/game-loop/system-ordering',
		'/guides/timestep-pause-timescale': '/build/game-loop/timestep-pause-timescale',
		'/engine/platform': '/build/game-loop/platform',
		'/engine/renderer': '/build/rendering',
		'/engine/renderer/sprites': '/build/rendering/sprites',
		'/engine/renderer/meshes': '/build/rendering/meshes',
		'/advanced/custom-rendering': '/build/rendering/custom-rendering',
		'/engine/input': '/build/input',
		'/advanced/input/multi-device': '/build/input/multi-device',
		'/advanced/input/remapping': '/build/input/remapping',
		'/engine/audio': '/build/audio',
		'/engine/assets': '/build/assets',
		'/guides/persistence': '/build/persistence',
		'/guides/debugging': '/build/debugging',
		'/advanced/change-tracking': '/understand/change-tracking',
		'/getting-started': '/start-here/wyrd-in-10-minutes',
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
				{
					label: 'Start Here',
					items: [
						{ label: 'New to Wyrd', slug: 'start-here/new-to-wyrd' },
						{ label: 'Already know ECS?', slug: 'start-here/already-know-ecs' },
						{ label: 'Wyrd in 10 minutes', slug: 'start-here/wyrd-in-10-minutes' },
					],
				},
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
						{
							label: 'Game Loop',
							items: [
								{ label: 'Platform', slug: 'build/game-loop/platform' },
								{ label: 'Systems', slug: 'build/game-loop/systems' },
								{ label: 'Command Buffer', slug: 'build/game-loop/systems/command-buffer' },
								{ label: 'System Ordering', slug: 'build/game-loop/system-ordering' },
								{ label: 'Timestep, Pause & Timescale', slug: 'build/game-loop/timestep-pause-timescale' },
							],
						},
						{
							label: 'Rendering',
							items: [
								{ label: 'Renderer', slug: 'build/rendering' },
								{ label: 'Sprites', slug: 'build/rendering/sprites' },
								{ label: 'Meshes', slug: 'build/rendering/meshes' },
								{ label: 'Custom Rendering', slug: 'build/rendering/custom-rendering' },
							],
						},
						{
							label: 'Input',
							items: [
								{ label: 'Input', slug: 'build/input' },
								{ label: 'Multi-Device', slug: 'build/input/multi-device' },
								{ label: 'Remapping', slug: 'build/input/remapping' },
							],
						},
						{ label: 'Audio', slug: 'build/audio' },
						{ label: 'Assets', slug: 'build/assets' },
						{ label: 'Persistence', slug: 'build/persistence' },
						{ label: 'Debugging', slug: 'build/debugging' },
					],
				},
				{
					label: 'Understand Wyrd',
					items: [
						{ label: 'ECS Architecture', slug: 'understand/ecs-architecture' },
						{ label: 'Queries', slug: 'understand/queries' },
						{ label: 'Scheduling', slug: 'understand/scheduling' },
						{ label: 'Parallel Execution', slug: 'understand/parallel-execution' },
						{ label: 'Structural Changes', slug: 'understand/structural-changes' },
						{ label: 'Relations', slug: 'understand/relations' },
						{ label: 'Change Tracking', slug: 'understand/change-tracking' },
						{ label: 'Persistence', slug: 'understand/persistence' },
						{ label: 'Source Generation', slug: 'understand/source-generation' },
					],
				},
				{ label: 'API Reference', slug: 'api-reference' },
			],
		}),
	],
});
