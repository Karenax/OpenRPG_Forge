// SPDX-License-Identifier: MIT
// Execute the frozen source's real PebbleField.update, without a scene draw.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { pathToFileURL } from 'node:url';

const [sourceArg, acquisitionArg, packageArg, outputArg] = process.argv.slice(2);
if (!outputArg) throw Error('SOURCE ACQUISITION GEOMETRY_PACKAGE NEW_OUTPUT required');
const [source, acquisition, geometryRoot, output] = [sourceArg, acquisitionArg, packageArg, outputArg].map(x => path.resolve(x));
const hash = data => crypto.createHash('sha256').update(data).digest('hex');
const git = (...args) => execFileSync('git', ['-c', `safe.directory=${source.replaceAll('\\', '/')}`, '-C', source, ...args], { encoding: 'utf8' }).trim();
const commit = '1438b1abfcaee3267092b75573014f4d9b4a983c';
const inventory = JSON.parse(fs.readFileSync(path.join(acquisition, 'inventory.json')));
const input = JSON.parse(fs.readFileSync(path.join(acquisition, 'payload-manifest.json')));
const rawPackage = fs.readFileSync(path.join(geometryRoot, 'package.json'));
if (hash(rawPackage) !== '0a84592f5ccaa0b8077849b6cf054935f6ecb33f41aaf04a19cec3b42255189a') throw Error('Geometry package differs');
const manifest = JSON.parse(rawPackage);
const checkSource = () => {
  if (git('rev-parse', 'HEAD') !== commit || git('status', '--porcelain', '--untracked-files=all')) throw Error('Clean pinned source required');
  for (const [name, pin] of Object.entries(inventory.source_inputs))
    if (hash(fs.readFileSync(path.join(source, name))) !== pin) throw Error(`Source changed: ${name}`);
};
checkSource();
if (inventory.source_commit !== commit || input.source_commit !== commit || fs.existsSync(output)) throw Error('Invalid input or existing output');
for (const parent of [source, acquisition, geometryRoot]) {
  const relative = path.relative(parent, output);
  if (!relative || (!relative.startsWith('..') && !path.isAbsolute(relative))) throw Error('Output must be external');
}
const read = (base, name, entry) => {
  if (path.basename(name) !== name || !entry) throw Error('Invalid acquisition path');
  const data = fs.readFileSync(path.join(base, name));
  if (data.length !== entry.bytes || hash(data) !== entry.sha256) throw Error(`Payload differs: ${name}`);
  return data;
};
const descriptor = JSON.parse(read(geometryRoot, manifest.pebble_field.file, manifest.pebble_field));
const at = name => import(pathToFileURL(path.join(source, name)).href);
await at('test/headless.mjs');
const { GPU } = await at('src/engine/gpu/GPU.js');
await GPU.init({ headless: true });
try {
  const [{ TerrainData }, { TerrainGPU }, { PebbleField }, { PerspectiveCamera, Scene }, { Village }, { Colliders }] = await Promise.all([
    at('src/world/TerrainData.js'), at('src/world/TerrainGPU.js'), at('src/world/debris/PebbleField.js'), at('src/engine/index.js'),
    at('src/world/Village.js'), at('src/world/Colliders.js'),
  ]);
  const terrain = new TerrainData(), gpu = new TerrainGPU(terrain, null);
  // Village flattens building footprints before the original field acquisition.
  new Village({ scene: new Scene(), terrain, colliders: new Colliders() });
  const mask = input.payloads['pebble-mask.rgba8.bin'];
  const field = new PebbleField({ terrain, gpu, mask: {
    data: new Uint8Array(read(acquisition, 'pebble-mask.rgba8.bin', mask)), res: mask.resolution, texel: mask.texel_m,
  } });
  for (const [key, values] of [['cell_flags', field.cellFlags], ['cell_min_y', field.cellMinY], ['cell_max_y', field.cellMaxY]]) {
    const expected = descriptor[key];
    const actual = Buffer.from(values.buffer, values.byteOffset, values.byteLength);
    if (!actual.equals(read(geometryRoot, expected.file, expected))) throw Error(`Reconstructed ${key} differs`);
  }
  if (field.occupiedCells !== 10415 || field.patchTris !== 4704) throw Error('Field population differs');
  const occupied = [...field.cellFlags.keys()].filter(i => field.cellFlags[i]);
  const cameras = [[0, 0]];
  for (let k = 0; k < 24; k++) {
    const cell = occupied[Math.floor(k * (occupied.length - 1) / 23)];
    cameras.push([terrain.origin + (cell % field.cellsPerSide + 0.5) * 4,
      terrain.origin + (Math.floor(cell / field.cellsPerSide) + 0.5) * 4]);
  }
  const cases = [];
  const camera = new PerspectiveCamera(60, 16 / 9, 0.1, 3000);
  for (const [x, z] of cameras) for (const yaw of [0, Math.PI / 2, Math.PI]) for (const height of [1.7, 25.001]) {
    const ground = terrain.heightAt(x, z), y = ground + height;
    camera.position.set(x, y, z);
    camera.lookAt(x + Math.sin(yaw) * 10, ground + 0.3, z - Math.cos(yaw) * 10);
    camera.updateMatrixWorld(true);
    // Reset only the source cache to capture every specified pose independently.
    field._last.fill(NaN);
    const changed = field.update(camera);
    const slots = [...field.arr.subarray(0, field.count * 4)];
    const skipped = field.update(camera) === false;
    if (!changed || !skipped || (height > 25 && field.count !== 0)) throw Error('Source update/cache contract differs');
    cases.push({ camera_position: [x, y, z], terrain_height: ground,
      camera_world: [...camera.matrixWorld.elements], projection: [...camera.projectionMatrix.elements],
      planes: field._frustum.planes.map(p => [...p.normal.toArray(), p.constant]),
      count: field.count, slots, unchanged_camera_skipped: skipped });
  }
  checkSource();
  const result = { schema: 'forge.tidewater.pebble-cell-reference', version: 1, source_commit: commit,
    source_inputs: inventory.source_inputs, geometry_package_sha256: hash(rawPackage),
    cell_flags_sha256: descriptor.cell_flags.sha256, cell_min_y_sha256: descriptor.cell_min_y.sha256,
    cell_max_y_sha256: descriptor.cell_max_y.sha256,
    cell_width_m: 4, radius_m: 25, maximum_cells: 240, patch_triangles: 4704,
    occupied_cells: field.occupiedCells, terrain_origin: terrain.origin, cells_per_side: field.cellsPerSide,
    cases, source_unchanged: true, rendered: false, rust_verified: false,
    limits: ['Actual source constructor and update; CPU cell selection only.',
      'No pebble vertex shader, material, draw, Rust adapter or visual parity qualification.'] };
  fs.mkdirSync(output);
  const bytes = Buffer.from(JSON.stringify(result, null, 2));
  fs.writeFileSync(path.join(output, 'reference.json'), bytes, { flag: 'wx' });
  console.log(JSON.stringify({ cases: cases.length, nonempty_cases: cases.filter(c => c.count).length,
    maximum_observed_cells: Math.max(...cases.map(c => c.count)), reference_sha256: hash(bytes),
    source_unchanged: true, rendered: false, rust_verified: false }));
} finally {
  GPU.device.destroy();
}
