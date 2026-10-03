import { cpSync, existsSync, mkdirSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { basename, dirname, relative, resolve } from 'node:path';
import { createHash } from 'node:crypto';

const COMMIT = '1438b1abfcaee3267092b75573014f4d9b4a983c';
const SOURCE_FILES = [
  'LICENSE',
  'public/models/characters/LICENSE-Rocketbox.md',
  'public/models/characters/joe.glb',
  'src/game/CatchDisplay.js',
  'src/game/FishStand.js',
  'src/game/FishTable.js',
  'src/game/Vendor.js',
  'src/world/fish/FishGeometry.js',
  'src/world/fish/FishMaterial.js',
  'src/world/fish/FishProps.js',
  'src/world/fish/FishSpecies.js',
];

function digest(bytes) { return createHash('sha256').update(bytes).digest('hex'); }
function record(path) {
  const bytes = readFileSync(path);
  return { bytes: bytes.length, sha256: digest(bytes) };
}
function die(message) { throw new Error(message); }

function copySource(root, output) {
  const copied = {};
  for (const name of SOURCE_FILES) {
    const source = resolve(root, name);
    if (!existsSync(source) || !statSync(source).isFile()) die(`missing Tidewater source: ${name}`);
    const target = resolve(output, 'source', name);
    mkdirSync(dirname(target), { recursive: true });
    cpSync(source, target, { force: false, errorOnExist: true });
    copied[`source/${name}`] = record(target);
  }
  return copied;
}

function exportContent(root, output) {
  if (!existsSync(resolve(root, '.git')) || !existsSync(resolve(root, 'src/game/FishStand.js'))) die('reference checkout required');
  if (existsSync(output)) die('output must be a new directory');
  mkdirSync(output, { recursive: true });
  const sourceFiles = copySource(root, output);
  const content = {
    schema: 'forge.tidewater.fish-stand-content-source-export', version: 1,
    source: { repository: 'https://github.com/dgreenheck/tidewater', commit: COMMIT },
    vendor: {
      id: 'tidewater:fish-stand:joe', name: 'Joe · Fish buyer', kind: 'buyer',
      placement: { stand_x: 49.9, stand_z: -74.6, stand_yaw_radians: 1.45, local_x: 0.2, local_y: 0.06, local_z: 0.12, y: 'terrain.heightAt(stand_x, stand_z) + local_y' },
      character: { file: 'source/public/models/characters/joe.glb', idle: 'idle_neutral_01', talk: 'gestic_talk_relaxed_01', greet: 'wave_01', license: 'MIT', license_file: 'source/public/models/characters/LICENSE-Rocketbox.md' },
      interaction: { radius_meters: 3.2, greeting: "Let's see what you caught. Fair prices, cash.", idle: 'Nothing to sell? The grunts are biting off the pier.' },
    },
    display: {
      fish_source: ['source/src/game/CatchDisplay.js', 'source/src/game/FishTable.js', 'source/src/world/fish/FishProps.js', 'source/src/world/fish/FishGeometry.js', 'source/src/world/fish/FishMaterial.js', 'source/src/world/fish/FishSpecies.js'],
      ice: { owner: 'StallKit.js', retained_in_bundle_005: true, local_center: [-0.55, 1.245, 0.86], width_meters: 0.92, depth_meters: 0.46, grid: [36, 22], material_layer: -1, status: 'geometry-present-in-fish-stand-005; source clearcoat procedural shading unported' },
      fish_on_ice: [
        { species: 'jack', length_meters: 0.36, local_x: -0.59, local_z: 0.7, local_yaw_radians: -0.1, pose: 'side' },
        { species: 'redSnapper', length_meters: 0.34, local_x: -0.51, local_z: 0.785, local_yaw_radians: 0.12, pose: 'sideFlip' },
        { species: 'yellowtail', length_meters: 0.3, local_x: -0.59, local_z: 0.87, local_yaw_radians: -0.1, pose: 'side' },
        { species: 'grunt', length_meters: 0.26, local_x: -0.51, local_z: 0.955, local_yaw_radians: 0.12, pose: 'sideFlip' },
        { species: 'mullet', length_meters: 0.33, local_x: -0.59, local_z: 1.04, local_yaw_radians: -0.1, pose: 'side' },
      ],
      source_rule: 'Each fish y = terrain.heightAt(stand_x, stand_z) + 1.27 + FishProps.restHeight(FISH[species].model, length_meters) + 0.006 * index.',
      material: { status: 'source-acquired-not-transposed', limitations: ['FishProps procedural geometry and FishMaterial wet-skin shader are not implemented in Bevy.', 'No native rendering, gameplay interaction, or pixel-equivalence claim.'] },
    },
    licenses: {
      tidewater_code: { spdx: 'MIT', file: 'source/LICENSE' },
      joe: { spdx: 'MIT', copyright: 'Microsoft, 2020', file: 'source/public/models/characters/LICENSE-Rocketbox.md' },
    },
    source_files: sourceFiles,
  };
  const target = resolve(output, 'content.json');
  writeFileSync(target, `${JSON.stringify(content, null, 2)}\n`);
  return { files: Object.keys(sourceFiles).length, manifest: record(target), joe: sourceFiles['source/public/models/characters/joe.glb'] };
}

function verify(output) {
  const manifestPath = resolve(output, 'content.json');
  const content = JSON.parse(readFileSync(manifestPath, 'utf8'));
  if (content.schema !== 'forge.tidewater.fish-stand-content-source-export' || content.version !== 1 || content.source.commit !== COMMIT) die('unknown content manifest');
  if (content.vendor.character.license !== 'MIT' || content.display.fish_on_ice.length !== 5 || content.display.ice.retained_in_bundle_005 !== true) die('content identity differs');
  for (const [name, expected] of Object.entries(content.source_files)) {
    const actual = record(resolve(output, name));
    if (actual.bytes !== expected.bytes || actual.sha256 !== expected.sha256) die(`source drift: ${name}`);
  }
  return { source_files: Object.keys(content.source_files).length, fish_on_ice: content.display.fish_on_ice.length, joe_bytes: content.source_files['source/public/models/characters/joe.glb'].bytes, manifest_sha256: record(manifestPath).sha256 };
}

const [mode, first, second] = process.argv.slice(2);
if (mode === '--export' && first && second && !process.argv[5]) console.log(JSON.stringify(exportContent(resolve(first), resolve(second))));
else if (mode === '--verify' && first && !second) console.log(JSON.stringify(verify(resolve(first))));
else die('usage: --export REFERENCE_ROOT NEW_OUTPUT | --verify OUTPUT');
