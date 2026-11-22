import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

// Built for GitHub Pages, where it lives under /claude-cli-protocol. Portuguese is the default
// language and lives at the root; the other four get a prefix.
export default defineConfig({
  site: 'https://carlosdealmeida.github.io',
  base: '/claude-cli-protocol',
  integrations: [
    starlight({
      title: 'claude-cli-protocol',
      description: 'The stream-json protocol of claude -p, specified, recorded and implemented.',
      defaultLocale: 'root',
      locales: {
        root: { label: 'Português', lang: 'pt-BR' },
        en: { label: 'English', lang: 'en' },
        es: { label: 'Español', lang: 'es' },
        'zh-cn': { label: '简体中文', lang: 'zh-CN' },
        'zh-tw': { label: '繁體中文', lang: 'zh-TW' },
      },
      social: [
        { icon: 'github', label: 'GitHub', href: 'https://github.com/carlosdealmeida/claude-cli-protocol' },
      ],
      sidebar: [
        {
          label: 'Visão geral',
          translations: { en: 'Overview', es: 'Visión general', 'zh-CN': '概览', 'zh-TW': '概觀' },
          slug: 'overview',
        },
      ],
    }),
  ],
});
