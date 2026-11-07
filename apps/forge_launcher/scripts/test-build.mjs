import { strict as assert } from 'node:assert';
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import {
  copyFileSync, lstatSync, mkdirSync, mkdtempSync, readFileSync, readdirSync,
  realpathSync, rmSync, rmdirSync, symlinkSync, unlinkSync, writeFileSync,
} from 'node:fs';
import { tmpdir } from 'node:os';
import { basename, dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const project = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const tempBase = realpathSync(tmpdir());
const fixture = mkdtempSync(join(tempBase, 'forge-launcher-profile-'));
const checkedFixture = realpathSync(fixture);
assert.equal(dirname(checkedFixture), tempBase);
assert.ok(basename(checkedFixture).startsWith('forge-launcher-profile-'));

let link;
let linkCreated = false;
try {
  const checkout = join(fixture, 'checkout');
  const launcher = join(checkout, 'apps', 'forge_launcher');
  const scripts = join(launcher, 'scripts');
  const source = join(launcher, 'canonical-ui');
  const root = join(fixture, '.build', 'tmp');
  const dist = join(launcher, 'dist');
  for (const path of [scripts, source, root, dist]) mkdirSync(path, { recursive: true });
  for (const name of ['build-aube.mjs', 'desktop.js']) copyFileSync(join(project, 'scripts', name), join(scripts, name));
  for (const name of ['index.html', 'styles.css']) copyFileSync(join(project, 'canonical-ui', name), join(source, name));
  const sentinel = join(dist, 'keep.txt');
  writeFileSync(sentinel, 'existing frontend to preserve');
  const hash = path => createHash('sha256').update(readFileSync(path)).digest('hex');
  const original = hash(sentinel);
  const script = join(scripts, 'build-aube.mjs');
  const cleanEnvironment = { ...process.env };
  for (const name of ['FORGE_LAUNCHER_PROFILE', 'FORGE_LAUNCHER_DIST', 'FORGE_LAUNCHER_TEMP_ROOT', 'FORGE_AUBE_SOURCE']) delete cleanEnvironment[name];
  const run = overrides => spawnSync(process.execPath, [script], {
    encoding: 'utf8', env: { ...cleanEnvironment, FORGE_LAUNCHER_PROFILE: 'standalone', ...overrides },
  });

  const output = join(root, 'standalone');
  let result = run({ FORGE_LAUNCHER_DIST: output, FORGE_LAUNCHER_TEMP_ROOT: root });
  assert.equal(result.status, 0, result.stderr);
  assert.match(result.stdout, /STANDALONE_FRONTEND_READY/);
  assert.deepEqual(readdirSync(output).sort(), ['desktop.js', 'index.html', 'styles.css']);
  const index = readFileSync(join(output, 'index.html'), 'utf8');
  const styles = readFileSync(join(output, 'styles.css'), 'utf8');
  assert.match(index, /data-forge-launcher="standalone"/);
  assert.doesNotMatch(index + styles, /L’Aube des royaumes|L’AUBE DES ROYAUMES|assets\/aube\.png|content\.js/);
  const outputHashes = readdirSync(output).sort().map(name => hash(join(output, name)));
  result = run({ FORGE_LAUNCHER_DIST: output, FORGE_LAUNCHER_TEMP_ROOT: root });
  assert.notEqual(result.status, 0, 'an existing output must never be overwritten');
  assert.deepEqual(readdirSync(output).sort().map(name => hash(join(output, name))), outputHashes);
  const defaultOutput = join(root, 'standalone-default');
  assert.equal(run({ FORGE_LAUNCHER_DIST: defaultOutput }).status, 0);
  assert.deepEqual(readdirSync(defaultOutput).sort(), ['desktop.js', 'index.html', 'styles.css']);
  const explicitRoot = join(fixture, 'another-temp');
  mkdirSync(explicitRoot);
  const explicitOutput = join(explicitRoot, 'standalone-explicit');
  assert.equal(run({ FORGE_LAUNCHER_DIST: explicitOutput, FORGE_LAUNCHER_TEMP_ROOT: explicitRoot }).status, 0);
  assert.deepEqual(readdirSync(explicitOutput).sort(), ['desktop.js', 'index.html', 'styles.css']);

  assert.notEqual(run({ FORGE_LAUNCHER_TEMP_ROOT: root }).status, 0);
  assert.notEqual(run({ FORGE_LAUNCHER_DIST: 'relative-output', FORGE_LAUNCHER_TEMP_ROOT: root }).status, 0);
  assert.notEqual(run({ FORGE_LAUNCHER_DIST: join(checkout, 'another-dist'), FORGE_LAUNCHER_TEMP_ROOT: root }).status, 0);
  assert.notEqual(run({ FORGE_LAUNCHER_DIST: `${root}/../tmp/escaped`, FORGE_LAUNCHER_TEMP_ROOT: root }).status, 0);
  assert.notEqual(run({ FORGE_LAUNCHER_DIST: join(root, 'nested', 'output'), FORGE_LAUNCHER_TEMP_ROOT: root }).status, 0);
  assert.notEqual(run({ FORGE_LAUNCHER_DIST: join(checkout, 'another-dist'), FORGE_LAUNCHER_TEMP_ROOT: checkout }).status, 0);
  const crateRoot = join(checkout, 'crates');
  mkdirSync(crateRoot);
  assert.notEqual(run({ FORGE_LAUNCHER_DIST: join(crateRoot, 'another-dist'), FORGE_LAUNCHER_TEMP_ROOT: crateRoot }).status, 0);
  assert.notEqual(run({ FORGE_LAUNCHER_DIST: join(fixture, 'another-dist'), FORGE_LAUNCHER_TEMP_ROOT: fixture }).status, 0);
  assert.notEqual(run({ FORGE_LAUNCHER_PROFILE: 'aube', FORGE_LAUNCHER_DIST: output }).status, 0);

  const target = join(fixture, 'link-target');
  mkdirSync(target);
  writeFileSync(join(target, 'keep.txt'), 'linked target to preserve');
  link = join(fixture, 'linked-temp');
  symlinkSync(target, link, process.platform === 'win32' ? 'junction' : 'dir');
  linkCreated = true;
  assert.notEqual(run({ FORGE_LAUNCHER_DIST: join(link, 'new'), FORGE_LAUNCHER_TEMP_ROOT: link }).status, 0);
  assert.equal(readFileSync(join(target, 'keep.txt'), 'utf8'), 'linked target to preserve');
  assert.equal(hash(sentinel), original);
  assert.deepEqual(readdirSync(dist), ['keep.txt']);
  console.log('PASS standalone frontend output isolation; no real UI observed');
} finally {
  if (linkCreated) {
    assert.ok(lstatSync(link).isSymbolicLink());
    if (process.platform === 'win32') rmdirSync(link);
    else unlinkSync(link);
  }
  // Only the verified fresh fixture is removed; no junction remains beneath it.
  const metadata = lstatSync(fixture);
  assert.ok(metadata.isDirectory() && !metadata.isSymbolicLink());
  assert.equal(realpathSync(fixture), checkedFixture);
  assert.equal(dirname(checkedFixture), tempBase);
  rmSync(checkedFixture, { recursive: true });
}
