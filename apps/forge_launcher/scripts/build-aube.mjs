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
import { dirname, isAbsolute, join, parse, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const project = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const checkout = resolve(project, '..', '..');
const workspace = resolve(checkout, '..');
const source = resolve(process.env.FORGE_AUBE_SOURCE || join(project, 'canonical-ui'));
const profile = process.env.FORGE_LAUNCHER_PROFILE || 'aube';
if (!['aube', 'standalone'].includes(profile)) throw new Error(`Unknown launcher profile: ${profile}`);

function normalizedAbsolute(value, label) {
  if (!value || !isAbsolute(value) || value.split(/[\\/]/).some(part => part === '.' || part === '..')) {
    throw new Error(`${label} must be an absolute normalized path`);
  }
  return resolve(value);
}

function contains(root, path) {
  const nested = relative(root, path);
  return nested === '' || (nested !== '..' && !nested.startsWith(`..${sep}`) && !isAbsolute(nested));
}

function requireOrdinaryDirectory(path) {
  const root = parse(path).root;
  let current = root;
  for (const part of path.slice(root.length).split(sep).filter(Boolean)) {
    current = join(current, part);
    const metadata = lstatSync(current);
    if (metadata.isSymbolicLink() || !metadata.isDirectory()) {
      throw new Error(`Temporary frontend root contains a link or non-directory: ${current}`);
    }
  }
}

let output;
if (profile === 'standalone') {
  const tempRoot = process.env.FORGE_LAUNCHER_TEMP_ROOT
    ? normalizedAbsolute(process.env.FORGE_LAUNCHER_TEMP_ROOT, 'FORGE_LAUNCHER_TEMP_ROOT')
    : resolve(workspace, '.build', 'tmp');
  output = normalizedAbsolute(process.env.FORGE_LAUNCHER_DIST, 'FORGE_LAUNCHER_DIST');
  if (contains(checkout, tempRoot) || contains(tempRoot, checkout) || relative(tempRoot, dirname(output)) !== '') {
    throw new Error('Standalone frontend must be a direct child of a temporary root outside the checkout');
  }
  requireOrdinaryDirectory(tempRoot);
  if (existsSync(output)) throw new Error('Standalone frontend output already exists');
} else {
  if (process.env.FORGE_LAUNCHER_DIST || process.env.FORGE_LAUNCHER_TEMP_ROOT) {
    throw new Error('Standalone output overrides cannot be used for the Aube profile');
  }
  output = resolve(project, 'dist');
  if (relative(project, output) !== 'dist' || (existsSync(output) && lstatSync(output).isSymbolicLink())) {
    throw new Error('Unsafe frontend output path');
  }
}
for (const name of profile === 'aube'
  ? ['index.html', 'styles.css', 'content.js', 'codex-core.js', 'app.js', 'assets']
  : ['index.html', 'styles.css']) {
  if (!existsSync(join(source, name))) throw new Error(`Missing Aube source: ${name}`);
}
const sourceHtml = readFileSync(join(source, 'index.html'), 'utf8');
if (!sourceHtml.includes('<title>OpenRPG Forge — L’Aube des royaumes</title>')) {
  throw new Error('The source is not the Aube launcher shown on port 8848');
}
if (!sourceHtml.includes('</head>')) throw new Error('The Aube HTML head is incomplete');

if (profile === 'standalone') {
  // Derive the game shell from the existing Aube layout and controls, while
  // excluding Aube-specific content, artwork, and scripts from the output.
  const replaceOnce = (text, from, to) => {
    if (text.split(from).length !== 2) throw new Error(`Launcher template marker changed: ${from}`);
    return text.replace(from, to);
  };
  let index = replaceOnce(sourceHtml, '<html lang="fr">', '<html lang="fr" data-forge-launcher="standalone">');
  index = replaceOnce(index, '<title>OpenRPG Forge — L’Aube des royaumes</title>', '<title>Jeu</title>');
  index = replaceOnce(index,
    '  <script defer src="content.js?v=5"></script>\n  <script defer src="codex-core.js?v=3"></script>\n  <script defer src="app.js?v=4"></script>',
    '  <script defer src="desktop.js"></script>');
  index = replaceOnce(index,
    '  <div class="landscape" aria-hidden="true"></div>\n  <div class="light-haze" aria-hidden="true"></div>\n', '');
  index = replaceOnce(index,
    '<a href="#accueil" class="brand" aria-label="OpenRPG Forge — Accueil"><span class="emblem" aria-hidden="true">✧</span><span>OPENRPG<small>FORGE</small></span></a>',
    '<a href="#accueil" class="brand" aria-label="Accueil"><span id="launcher-brand">Jeu</span></a>');
  index = replaceOnce(index,
    '<a href="#accueil">Accueil</a><a href="#aventures">Jouer</a><a href="#multijoueur">Multijoueur</a><a href="#guide">Guide du joueur</a><a href="#bestiaire">Bestiaire</a><a href="#reglages">Réglages</a>',
    '<a href="#accueil">Accueil</a><a href="#aventures">Jouer</a><a href="#reglages">Réglages</a>');
  index = replaceOnce(index, '    <button id="motion" class="quiet-button" aria-pressed="false">Fond animé</button>\n', '');
  index = replaceOnce(index, '<main id="main" tabindex="-1"></main>',
    '<main id="main" tabindex="-1"><section class="utility"><h1>Votre jeu</h1><p role="status">Vérification du jeu installé…</p></section></main>');
  index = replaceOnce(index,
    '<footer class="site-footer"><span>OPENRPG FORGE <b>·</b> L’AUBE DES ROYAUMES</span><span>Interface exploratoire · documentation de conception</span><a href="#a-propos">Sources et limites</a></footer>',
    '<footer class="site-footer"><span id="launcher-footer">Jeu</span></footer>');
  index = replaceOnce(index,
    '<noscript><p class="notice">Activez JavaScript pour consulter les fiches et utiliser la recherche locale.</p></noscript>',
    '<noscript><p class="notice">Activez JavaScript pour ouvrir le jeu.</p></noscript>');
  const originalStyles = readFileSync(join(source, 'styles.css'), 'utf8');
  const styles = replaceOnce(originalStyles, "url('assets/aube.png') center/cover", '#10201d');
  if (/L’Aube des royaumes|L’AUBE DES ROYAUMES|assets\/aube\.png|content\.js/.test(index + styles)) {
    throw new Error('Standalone shell contains Aube content');
  }
  const desktop = readFileSync(join(project, 'scripts/desktop.js'));
  // Create only a fresh directory and fresh files; never remove or overwrite
  // a prior build, even when the caller accidentally repeats the same path.
  mkdirSync(output);
  writeFileSync(join(output, 'index.html'), index, { flag: 'wx' });
  writeFileSync(join(output, 'styles.css'), styles, { flag: 'wx' });
  writeFileSync(join(output, 'desktop.js'), desktop, { flag: 'wx' });
  console.log(`STANDALONE_FRONTEND_READY output=${output}`);
} else {
rmSync(output, { recursive: true, force: true });
mkdirSync(output, { recursive: true });
const index = sourceHtml.replace('</head>', '  <script defer src="desktop.js"></script>\n  <script defer src="aube.js"></script>\n</head>');
writeFileSync(join(output, 'index.html'), index);
for (const name of ['styles.css', 'codex-core.js', 'app.js']) {
  copyFileSync(join(source, name), join(output, name));
}
const content = readFileSync(join(source, 'content.js'), 'utf8');
writeFileSync(join(output, 'content.js'), content.replaceAll('E:\\\\OpenRPG_Forge\\\\', ''));
copyFileSync(join(project, 'scripts/desktop.js'), join(output, 'desktop.js'));
copyFileSync(join(project, 'scripts/aube.js'), join(output, 'aube.js'));
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
}
