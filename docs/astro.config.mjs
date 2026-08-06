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
	integrations: [
		starlight({
			title: 'Wyrd.Ecs',
			logo: { src: './src/assets/wyrd-mark.svg', alt: 'Wyrd.Ecs' },
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
			social: [{ icon: 'github', label: 'GitHub', href: 'https://github.com/withastro/starlight' }],
			sidebar: [
				{
					label: 'Guides',
					items: [
						// Each item here is one entry in the navigation menu.
						{ label: 'Example Guide', slug: 'guides/example' },
					],
				},
				{
					label: 'Reference',
					items: [{ autogenerate: { directory: 'reference' } }],
				},
			],
		}),
	],
});
