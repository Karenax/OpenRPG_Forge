import { createHash } from 'node:crypto';
import {
  copyFileSync,
  cpSync,
  existsSync,
  lstatSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  rmSync,
  writeFileSync,
} from 'node:fs';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const project = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const source = resolve(process.env.FORGE_AUBE_SOURCE || join(project, '../../../launcher-aube'));
const output = resolve(project, 'dist');

if (relative(project, output) !== 'dist' || (existsSync(output) && lstatSync(output).isSymbolicLink())) {
  throw new Error('Unsafe frontend output path');
}
for (const name of ['index.html', 'styles.css', 'content.js', 'codex-core.js', 'app.js', 'assets']) {
  if (!existsSync(join(source, name))) throw new Error(`Missing Aube source: ${name}`);
}
const sourceHtml = readFileSync(join(source, 'index.html'), 'utf8');
if (!sourceHtml.includes('<title>OpenRPG Forge — L’Aube des royaumes</title>')) {
  throw new Error('The source is not the Aube launcher shown on port 8848');
}
if (!sourceHtml.includes('</head>')) throw new Error('The Aube HTML head is incomplete');

rmSync(output, { recursive: true, force: true });
mkdirSync(output, { recursive: true });
const index = sourceHtml.replace('</head>', '  <script defer src="desktop.js"></script>\n</head>');
writeFileSync(join(output, 'index.html'), index);
for (const name of ['styles.css', 'codex-core.js', 'app.js']) {
  copyFileSync(join(source, name), join(output, name));
}
const content = readFileSync(join(source, 'content.js'), 'utf8');
writeFileSync(join(output, 'content.js'), content.replaceAll('E:\\\\OpenRPG_Forge\\\\', ''));
copyFileSync(join(project, 'scripts/desktop.js'), join(output, 'desktop.js'));
cpSync(join(source, 'assets'), join(output, 'assets'), { recursive: true });
mkdirSync(join(output, 'sources'));
copyFileSync(join(source, 'sources/bestiary-research.md'), join(output, 'sources/bestiary-research.md'));
const manifest = JSON.parse(readFileSync(join(source, 'sources/manifest.json'), 'utf8'));
for (const entry of manifest.sources) {
  const value = String(entry.path).replaceAll('\\', '/');
  const root = value.indexOf('/OpenRPG_Forge/');
  entry.path = root >= 0 ? value.slice(root + 1) : `local-source/${value.split('/').at(-1)}`;
}
writeFileSync(join(output, 'sources/manifest.json'), `${JSON.stringify(manifest, null, 2)}\n`);

const images = readdirSync(join(output, 'assets'), { recursive: true, withFileTypes: true })
  .filter(entry => entry.isFile());
const sha256 = value => createHash('sha256').update(value).digest('hex');
const sourceHash = sha256(sourceHtml);
const copiedHash = sha256(readFileSync(join(output, 'styles.css')));
if (copiedHash !== sha256(readFileSync(join(source, 'styles.css')))) {
  throw new Error('The Aube styles changed while copying');
}
if (images.length < 68) throw new Error(`Expected at least 68 Aube illustrations, got ${images.length}`);
if (readFileSync(join(output, 'content.js'), 'utf8').includes('E:\\\\OpenRPG_Forge')) {
  throw new Error('A local author path remains in the packaged content');
}
console.log(`AUBE_FRONTEND_READY source_html_sha256=${sourceHash} images=${images.length}`);
