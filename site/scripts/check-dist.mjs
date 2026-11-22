// Checks the built site: the five locales, the language of each page and the links under the base path.
// Usage (after `npm run build`): node scripts/check-dist.mjs
import { existsSync, readFileSync, statSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const DIST = path.join(path.dirname(fileURLToPath(import.meta.url)), '..', 'dist');
const BASE = '/claude-cli-protocol/';
const LOCALES = [
  { path: '', lang: 'pt-BR' },
  { path: 'en/', lang: 'en' },
  { path: 'es/', lang: 'es' },
  { path: 'zh-cn/', lang: 'zh-CN' },
  { path: 'zh-tw/', lang: 'zh-TW' },
];
const PAGES = ['index.html', 'overview/index.html'];

const problems = [];
let pages = 0;

for (const locale of LOCALES) {
  for (const page of PAGES) {
    const rel = locale.path + page;
    const file = path.join(DIST, rel);
    if (!existsSync(file)) {
      problems.push(`missing page: dist/${rel}`);
      continue;
    }
    pages++;
    const html = readFileSync(file, 'utf8');

    const lang = /<html[^>]*\slang="([^"]*)"/.exec(html)?.[1];
    if (lang !== locale.lang) problems.push(`dist/${rel}: lang is "${lang}", expected "${locale.lang}"`);

    // On GitHub Pages the site lives under BASE: a link that starts at "/" and not at BASE leaves the site.
    for (const match of html.matchAll(/\shref="(\/[^"]*)"/g)) {
      const href = match[1];
      if (href.startsWith('//')) continue; // protocol-relative: another host
      if (!href.startsWith(BASE) && href !== BASE.slice(0, -1)) {
        problems.push(`dist/${rel}: link outside the base path: ${href}`);
        continue;
      }
      // and it must lead somewhere: a file of dist, or a folder with an index.html
      const target = decodeURIComponent(href.slice(BASE.length).split(/[?#]/)[0]);
      const found = [target, path.join(target, 'index.html')].some((candidate) => {
        const full = path.join(DIST, candidate);
        return existsSync(full) && statSync(full).isFile();
      });
      if (!found) problems.push(`dist/${rel}: dead link: ${href}`);
    }
  }
}

if (problems.length > 0) {
  for (const line of [...new Set(problems)]) console.error(line);
  process.exit(1);
}
console.log(`site ok: ${LOCALES.length} locales, ${pages} pages`);
