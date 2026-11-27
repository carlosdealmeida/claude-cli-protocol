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
        {
          label: 'Especificação',
          translations: { en: 'Specification', es: 'Especificación', 'zh-CN': '规范', 'zh-TW': '規格' },
          items: [
            { label: 'Protocolo', translations: { en: 'Protocol', es: 'Protocolo', 'zh-CN': '协议', 'zh-TW': '通訊協定' }, slug: 'spec/protocolo' },
            { label: 'Comandos', translations: { en: 'Commands', es: 'Comandos', 'zh-CN': '命令', 'zh-TW': '命令' }, slug: 'spec/comandos' },
            { label: 'Controle', translations: { en: 'Control', es: 'Control', 'zh-CN': '控制', 'zh-TW': '控制' }, slug: 'spec/controle' },
            { label: 'Transporte', translations: { en: 'Transport', es: 'Transporte', 'zh-CN': '传输层', 'zh-TW': '傳輸層' }, slug: 'spec/transporte' },
            { label: 'Eventos', translations: { en: 'Events', es: 'Eventos', 'zh-CN': '事件', 'zh-TW': '事件' }, slug: 'spec/eventos' },
            { label: 'Fixtures', translations: { en: 'Fixtures', es: 'Fixtures', 'zh-CN': 'Fixtures', 'zh-TW': 'Fixtures' }, slug: 'spec/fixtures' },
          ],
        },
      ],
    }),
  ],
});
