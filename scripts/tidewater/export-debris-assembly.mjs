// SPDX-License-Identifier: MIT
// Tidewater source copyright 2026 DRG Software Solutions LLC, commit 1438b1ab.
// Acquire audited source assembly and shader factories; no upstream writes or scan fetch.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { pathToFileURL } from 'node:url';

const [sourceArg, acquisitionArg, outputArg] = process.argv.slice(2);
if (!sourceArg || !acquisitionArg || !outputArg) throw Error('SOURCE ACQUISITION NEW_OUTPUT required');
const source = path.resolve(sourceArg), acquisition = path.resolve(acquisitionArg), output = path.resolve(outputArg);
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const git = (...args) => execFileSync('git', ['-c', `safe.directory=${source.replaceAll('\\', '/')}`,
  '-C', source, ...args], { encoding: 'utf8' }).trim();
const inventoryBytes = fs.readFileSync(path.join(acquisition, 'inventory.json'));
const inventory = JSON.parse(inventoryBytes);
const inputManifest = JSON.parse(fs.readFileSync(path.join(acquisition, 'payload-manifest.json')));
const pin = '1438b1abfcaee3267092b75573014f4d9b4a983c';
if (inventory.source_commit !== pin || git('rev-parse', 'HEAD') !== pin || git('status', '--porcelain', '--untracked-files=all'))
  throw Error('Clean pinned source required');
if (inputManifest.byte_order !== 'little-endian' || new Uint8Array(new Uint32Array([1]).buffer)[0] !== 1)
  throw Error('Little-endian inputs and host required');
for (const root of [source, acquisition]) {
  const rel = path.relative(root, output);
  if (!rel || (!rel.startsWith('..') && !path.isAbsolute(rel))) throw Error('Output must be external');
}
if (fs.existsSync(output)) throw Error('Fresh output required');
const checkSource = () => {
  for (const [name, digest] of Object.entries(inventory.source_inputs))
    if (hash(fs.readFileSync(path.join(source, name))) !== digest) throw Error('Source changed: ' + name);
  if (git('status', '--porcelain', '--untracked-files=all')) throw Error('Source is modified');
};
checkSource();
const read = name => {
  if (path.basename(name) !== name) throw Error('Unsafe input path');
  const data = fs.readFileSync(path.join(acquisition, name)), expected = inputManifest.payloads[name];
  if (!expected || data.length !== expected.bytes || hash(data) !== expected.sha256) throw Error('Invalid payload: ' + name);
  return data;
};
const at = name => import(pathToFileURL(path.join(source, name)).href);
await at('test/headless.mjs');
const { GPU } = await at('src/engine/gpu/GPU.js');
await GPU.init({ headless: true });
try {
  const [{ TerrainData }, { TerrainGPU }, { Village }, { Scene, Group }, { Colliders },
    { Builder }, { Debris }, { PebbleField, PCELL, R_FAR }, { collectModules }] = await Promise.all([
    at('src/world/TerrainData.js'), at('src/world/TerrainGPU.js'), at('src/world/Village.js'),
    at('src/engine/index.js'), at('src/world/Colliders.js'), at('src/world/village/GeoBuilder.js'),
    at('src/world/Debris.js'), at('src/world/debris/PebbleField.js'), at('src/engine/gpu/Shader.js'),
  ]);
  const terrain = new TerrainData(), gpu = new TerrainGPU(terrain, null);
  const village = new Village({ scene: new Scene(), terrain, colliders: new Colliders() });
  const B = new Builder();
  for (const [name, entry] of Object.entries(inventory.batches)) {
    const b = B.batch(name);
    for (const key of ['pos', 'nrm', 'uv', 'tint', 'data']) {
      const raw = read(`${name}.${key}.f64.bin`);
      b[key] = Array.from({ length: raw.length / 8 }, (_, i) => raw.readDoubleLE(i * 8));
    }
    const raw = read(`${name}.indices.u32.bin`);
    b.idx = Array.from({ length: raw.length / 4 }, (_, i) => raw.readUInt32LE(i * 4));
    b.vcount = entry.vertices;
  }
  const assembled = Object.create(Debris.prototype);
  Object.assign(assembled, { terrainData: terrain, gpu, group: new Group() });
  assembled._assemble(B, village);
  const maskDescriptor = inputManifest.payloads['pebble-mask.rgba8.bin'];
  const pebbles = new PebbleField({ terrain, gpu, mask: { data: new Uint8Array(read('pebble-mask.rgba8.bin')),
    res: maskDescriptor.resolution, texel: maskDescriptor.texel_m } });
  const files = {}, pending = {};
  const save = (name, bytes) => {
    if (!Buffer.isBuffer(bytes)) bytes = Buffer.from(bytes);
    if (pending[name] && !pending[name].equals(bytes)) throw Error('Payload collision');
    pending[name] = bytes; files[name] = { bytes: bytes.length, sha256: hash(bytes) };
    return { file: name, ...files[name] };
  };
  const binary = (name, array) => save(name, Buffer.from(array.buffer, array.byteOffset, array.byteLength));
  const geometry = (name, geo) => ({
    attributes: Object.fromEntries(Object.entries(geo.attributes).map(([key, value]) => [key, {
      ...binary(`${name}-${key}.bin`, value.array), components: value.itemSize,
      scalar: value.array.constructor.name, count: value.count,
    }])),
    indices: { ...binary(`${name}-indices.bin`, geo.index.array), scalar: geo.index.array.constructor.name,
      count: geo.index.count },
  });
  const uniform = block => block ? { struct: block.structName, bytes: block.byteLength, wgsl: block.wgsl,
    fields: Object.fromEntries(block.order.map(name => [name, {
      type: block.layout[name].typeStr, offset: block.layout[name].offset,
      value: block.fields[name].value?.toArray?.() ?? block.fields[name].value,
    }])) } : null;
  const bindings = values => Object.fromEntries(Object.entries(values).map(([name, binding]) => {
    if (binding.uniform) return [name, { uniform: uniform(binding.uniform) }];
    if (binding.texture) {
      const texture = typeof binding.texture === 'function' ? binding.texture() : binding.texture;
      return [name, { texture: { label: texture.label, width: texture.width, height: texture.height,
        format: texture.format }, sample_type: binding.sampleType ?? null, pixels_retained_here: false }];
    }
    return [name, { unresolved: true }];
  }));
  const materials = {};
  for (const [name, material] of Object.entries({ wood: village.materials.wood, hard: village.materials.hard,
    fabric: village.materials.fabric, nature: assembled.materials.nature, pebbles: pebbles.material })) {
    materials[name] = { name: material.name, attributes: material.attributes, varyings: material.varyings,
      defines: material.defines, side: material.side, transparent: material.transparent,
      depth_write: material.depthWrite, alpha_test: material.alphaTest,
      uniforms: uniform(material.uniformBlock), bindings: bindings(material.bindings),
      stages: Object.fromEntries(['vertex', 'surface', 'shadow'].map(stage => [stage,
        save(`${name}-${stage}.wgsl`, material[stage] ?? '')])),
      modules: collectModules(material.modules).map(module => ({ name: module.name,
        dependencies: module.deps.map(dep => dep.name), bindings: bindings(module.bindings),
        code: save(`module-${hash(Buffer.from(module.code))}.wgsl`, module.code) })),
    };
  }
  const meshes = assembled.meshes.map(mesh => ({ name: mesh.name, cast_shadow: mesh.castShadow,
    receive_shadow: mesh.receiveShadow, draw_range: mesh.geometry.drawRange,
    geometry: geometry(mesh.name, mesh.geometry) }));
  const pebble = { PCELL, R_FAR, occupied_cells: pebbles.occupiedCells, cells_per_side: pebbles.cellsPerSide,
    patch_triangles: pebbles.patchTris, geometry: geometry('pebbles', pebbles.geometry),
    cell_flags: binary('pebble-cell-flags.bin', pebbles.cellFlags),
    cell_min_y: binary('pebble-cell-min-y.bin', pebbles.cellMinY),
    cell_max_y: binary('pebble-cell-max-y.bin', pebbles.cellMaxY),
    mask: save('pebble-mask.rgba8.bin', read('pebble-mask.rgba8.bin')) };
  save('LICENSE-Tidewater.txt', fs.readFileSync(path.join(source, 'LICENSE')));
  save('CREDITS-scanned.md', fs.readFileSync(path.join(source, 'public/models/debris/CREDITS.md')));
  checkSource();
  const descriptor = { schema: 'forge.tidewater.debris-source-assembly', version: 1, source_commit: pin,
    source_inventory_sha256: hash(inventoryBytes), source_payload_manifest_sha256:
      hash(fs.readFileSync(path.join(acquisition, 'payload-manifest.json'))), materials, meshes, pebble,
    nature_caster_triangles: assembled.natureCasterTris, shadow_triangles: assembled.shadowTriangles, files,
    limits: ['Actual source assembly and shader factories, no scene draw or Rust adaptation.',
      'TerrainGPU uses its unbaked initial sun shadow and placeholder shore field for contract acquisition.',
      'Material texture pixels are not acquired here; binding contracts only.',
      'Scanned models and camera-following cell update are not executed.'] };
  fs.mkdirSync(output);
  for (const [name, bytes] of Object.entries(pending)) fs.writeFileSync(path.join(output, name), bytes, { flag: 'wx' });
  fs.writeFileSync(path.join(output, 'assembly.json'), JSON.stringify(descriptor, null, 2), { flag: 'wx' });
  console.log(JSON.stringify({ meshes: meshes.length, materials: Object.keys(materials), payloads: Object.keys(files).length,
    nature_caster_triangles: assembled.natureCasterTris, shadow_triangles: assembled.shadowTriangles,
    pebble_patch_triangles: pebbles.patchTris, occupied_cells: pebbles.occupiedCells,
    source_unchanged: true, rendered: false }));
} finally {
  GPU.device.destroy();
}
