import { mkdirSync, readFileSync, writeFileSync, existsSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { createHash } from 'node:crypto';

const COMMIT = '1438b1abfcaee3267092b75573014f4d9b4a983c';
const DISPLAY = [
  ['jack', 0.36, -0.59, 0.70, -0.10, 'side'],
  ['redSnapper', 0.34, -0.51, 0.785, 0.12, 'sideFlip'],
  ['yellowtail', 0.30, -0.59, 0.87, -0.10, 'side'],
  ['grunt', 0.26, -0.51, 0.955, 0.12, 'sideFlip'],
  ['mullet', 0.33, -0.59, 1.04, -0.10, 'side'],
];
const sha = bytes => createHash('sha256').update(bytes).digest('hex');
const record = path => { const bytes = readFileSync(path); return { file: path.split(/[\\/]/).at(-1), bytes: bytes.length, sha256: sha(bytes) }; };
const write = (path, value) => { mkdirSync(dirname(path), { recursive: true }); writeFileSync(path, value); return record(path); };
const floats = values => Buffer.from(values.buffer, values.byteOffset, values.byteLength);
function die(message) { throw new Error(message); }

async function exportFish(reference, output) {
  if (existsSync(output)) die('output must be new');
  const root = resolve(reference);
  const geometryUrl = new URL(`file:///${root.replaceAll('\\', '/')}/src/world/fish/FishGeometry.js`);
  const speciesUrl = new URL(`file:///${root.replaceAll('\\', '/')}/src/world/fish/FishSpecies.js`);
  const { fishGeometry, section } = await import(geometryUrl);
  const { SPECIES } = await import(speciesUrl);
  const fish = [];
  for (let index = 0; index < DISPLAY.length; index += 1) {
    const [species, length_meters, local_x, local_z, local_yaw_radians, pose] = DISPLAY[index];
    const model = SPECIES[species];
    if (!model) die(`missing species: ${species}`);
    const mesh = fishGeometry(model, { lod: 0, pose: 'dead', eyes: true });
    const position = mesh.attributes.position?.array, normal = mesh.attributes.normal?.array, data = mesh.attributes.aData?.array, sourceIndices = mesh.index?.array;
    const indices = sourceIndices && Uint32Array.from(sourceIndices);
    if (!(position && normal && data && indices) || position.length / 3 !== normal.length / 3 || position.length / 3 !== data.length / 4) die(`invalid geometry: ${species}`);
    const dir = resolve(output, species);
    const positionEntry = write(resolve(dir, 'position.f32le.bin'), floats(position));
    const normalEntry = write(resolve(dir, 'normal.f32le.bin'), floats(normal));
    const dataEntry = write(resolve(dir, 'a-data.f32le.bin'), floats(data));
    const indexEntry = write(resolve(dir, 'indices.u32le.bin'), floats(indices));
    let halfThickness = 0;
    for (let u = 0.1; u < 0.9; u += 0.05) halfThickness = Math.max(halfThickness, section(model, u).W);
    fish.push({ species, length_meters, local_x, local_z, local_yaw_radians, pose, index,
      rest_height_meters: halfThickness * length_meters * 0.85,
      geometry: { position: { ...positionEntry, item_size: 3, count: position.length / 3 }, normal: { ...normalEntry, item_size: 3, count: normal.length / 3 }, a_data: { ...dataEntry, item_size: 4, count: data.length / 4 }, indices: { ...indexEntry, item_size: 4, count: indices.length }, triangles: indices.length / 3 },
    });
  }
  const manifest = { schema: 'forge.tidewater.fish-stand-fish-source-export', version: 1, source: { repository: 'https://github.com/dgreenheck/tidewater', commit: COMMIT }, source_files: {
    'FishGeometry.js': record(resolve(root, 'src/world/fish/FishGeometry.js')),
    'FishSpecies.js': record(resolve(root, 'src/world/fish/FishSpecies.js')),
    'FishStand.js': record(resolve(root, 'src/game/FishStand.js')),
    'CatchDisplay.js': record(resolve(root, 'src/game/CatchDisplay.js')),
    'LICENSE': record(resolve(root, 'LICENSE')),
  }, fish, material: { status: 'geometry-transposed-standard-material-not-source-parity', limitation: 'The native adapter retains source geometry and placement but not FishMaterial wet-skin/WGSL, LOD or animation.' } };
  write(resolve(output, 'fish.json'), Buffer.from(`${JSON.stringify(manifest, null, 2)}\n`));
  return { fish: fish.length, triangles: fish.reduce((sum, item) => sum + item.geometry.triangles, 0), manifest_sha256: record(resolve(output, 'fish.json')).sha256 };
}
function verify(output) {
  const manifest = JSON.parse(readFileSync(resolve(output, 'fish.json'), 'utf8'));
  if (manifest.schema !== 'forge.tidewater.fish-stand-fish-source-export' || manifest.source.commit !== COMMIT || manifest.fish.length !== 5) die('invalid manifest');
  for (const item of manifest.fish) {
    const index = item.geometry.indices;
    if (index.item_size !== 4 || index.bytes !== index.count * 4 || index.count % 3 || index.count / 3 !== item.geometry.triangles) die(`invalid u32 indices: ${item.species}`);
    for (const part of Object.values(item.geometry)) {
    if (part.file) { const actual = record(resolve(output, item.species, part.file)); if (actual.bytes !== part.bytes || actual.sha256 !== part.sha256) die(`drift: ${item.species}/${part.file}`); }
    }
  }
  return { fish: manifest.fish.length, triangles: manifest.fish.reduce((sum, item) => sum + item.geometry.triangles, 0), manifest_sha256: record(resolve(output, 'fish.json')).sha256 };
}
const [mode, first, second] = process.argv.slice(2);
if (mode === '--export' && first && second) console.log(JSON.stringify(await exportFish(first, second)));
else if (mode === '--verify' && first && !second) console.log(JSON.stringify(verify(first)));
else die('usage: --export REFERENCE NEW_OUTPUT | --verify OUTPUT');
