// SPDX-License-Identifier: MIT
// Tidewater source copyright 2026 DRG Software Solutions LLC, commit 1438b1ab.
// Acquire the audited GLB parser's geometry and source instance transform convention.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { pathToFileURL } from 'node:url';

const [sourceArg, scansArg, inventoryArg, outputArg] = process.argv.slice(2);
if (!sourceArg || !scansArg || !inventoryArg || !outputArg) throw Error('SOURCE SCANS INVENTORY NEW_OUTPUT required');
const source = path.resolve(sourceArg), scans = path.resolve(scansArg), output = path.resolve(outputArg);
const hash = data => crypto.createHash('sha256').update(data).digest('hex');
const inventory = JSON.parse(fs.readFileSync(path.join(inventoryArg, 'inventory.json')));
const sourceBytes = fs.readFileSync(path.join(scans, 'scanned-source.json'));
const retained = JSON.parse(sourceBytes);
const pin = '1438b1abfcaee3267092b75573014f4d9b4a983c';
const git = (...args) => execFileSync('git', ['-c', `safe.directory=${source.replaceAll('\\', '/')}`,
  '-C', source, ...args], { encoding: 'utf8' }).trim();
if (git('rev-parse', 'HEAD') !== pin || git('status', '--porcelain', '--untracked-files=all')
  || inventory.source_commit !== pin || retained.source_commit !== pin) throw Error('Clean pinned source required');
if (hash(fs.readFileSync(path.join(inventoryArg, 'inventory.json'))) !== retained.source_inventory_sha256)
  throw Error('Placement inventory differs');
if (fs.existsSync(output)) throw Error('Fresh output required');
for (const root of [source, scans, path.resolve(inventoryArg)]) {
  const relative = path.relative(root, output);
  if (!relative || (!relative.startsWith('..') && !path.isAbsolute(relative))) throw Error('External output required');
}
const checkSource = () => {
  for (const name of ['src/world/debris/GLB.js', 'src/world/debris/ScannedDebris.js'])
    if (hash(fs.readFileSync(path.join(source, name))) !== inventory.source_inputs[name]) throw Error('Source contract differs');
  if (git('status', '--porcelain', '--untracked-files=all')) throw Error('Source changed');
};
checkSource();
const read = name => {
  if (path.basename(name) !== name) throw Error('Unsafe input name');
  const bytes = fs.readFileSync(path.join(scans, name)), expected = retained.files[name];
  if (!expected || bytes.length !== expected.bytes || hash(bytes) !== expected.sha256) throw Error('Retained input differs');
  return bytes;
};
const at = name => import(pathToFileURL(path.join(source, name)).href);
await at('test/headless.mjs');
const { GPU } = await at('src/engine/gpu/GPU.js');
await GPU.init({ headless: true });
try {
  const [{ parseGLB }, { SCAN_ASSETS, SCAN_SIZE }, { Vector3, Quaternion, Euler, Matrix4 }] = await Promise.all([
    at('src/world/debris/GLB.js'), at('src/world/debris/ScannedDebris.js'), at('src/engine/index.js'),
  ]);
  const files = {}, pending = {};
  const save = (name, bytes) => {
    pending[name] = bytes; files[name] = { bytes: bytes.length, sha256: hash(bytes) };
    return { file: name, ...files[name] };
  };
  const binary = (name, array) => save(name, Buffer.from(array.buffer, array.byteOffset, array.byteLength));
  const meshes = [];
  if (new Uint8Array(new Uint32Array([1]).buffer)[0] !== 1) throw Error('Little-endian acquisition required');
  for (const [ordinal, name] of SCAN_ASSETS.entries()) {
    if (retained.assets[ordinal].source_name !== name) throw Error('Source asset order differs');
    const bytes = read(name + '.glb');
    const parsed = parseGLB(bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength));
    if (parsed.meshes.length !== 3) throw Error('Expected exactly three source LOD meshes');
    for (let lod = 0; lod < 3; lod++) {
      const found = parsed.meshes.find(mesh => mesh.name.startsWith('LOD' + lod));
      if (!found) throw Error('Source LOD absent');
      const geo = found.geometry, attributes = {};
      for (const [key, width] of Object.entries({ position: 3, normal: 3, uv: 2 })) {
        const attr = geo.attributes[key];
        if (!attr || attr.itemSize !== width || attr.count !== geo.attributes.position.count
          || !Array.from(attr.array).every(Number.isFinite)) throw Error('Invalid source GLB attribute');
        attributes[key] = { ...binary(`asset-${ordinal}-lod-${lod}-${key}.bin`, attr.array),
          components: width, vertices: attr.count, scalar: 'float32' };
      }
      const index = new Uint32Array(geo.index.array);
      if (!index.length || index.length % 3 || index.some(i => i >= geo.attributes.position.count))
        throw Error('Invalid source GLB indices');
      // Preserve both original UVs and the exact source atlas clamp/tile mapping.
      const atlasUv = new Float32Array(geo.attributes.uv.array);
      for (let i = 0; i < atlasUv.length; i += 2) {
        atlasUv[i] = Math.min(.998, Math.max(.002, atlasUv[i])) * .5 + (ordinal % 2) * .5;
        atlasUv[i + 1] = Math.min(.998, Math.max(.002, atlasUv[i + 1])) * .5 + Math.floor(ordinal / 2) * .5;
      }
      meshes.push({ asset: ordinal, source_name: name, lod, node: found.name,
        attributes, atlas_uv: binary(`asset-${ordinal}-lod-${lod}-atlas-uv.bin`, atlasUv),
        indices: binary(`asset-${ordinal}-lod-${lod}-indices-u32.bin`, index), triangles: index.length / 3,
        node_transforms_applied_by_source_parser: true });
    }
  }
  const instances = retained.instances.map((r, ordinal) => {
    // Source constructor uses Euler(roll, yaw, pitch, 'YXZ'), not XYZ or yaw/pitch/roll.
    const q = new Quaternion().setFromEuler(new Euler(r.roll || 0, r.yaw, r.pitch || 0, 'YXZ'));
    const matrix = new Matrix4().compose(new Vector3(r.x, r.y, r.z), q, new Vector3(r.sx, r.sy, r.sz));
    const size = SCAN_SIZE[r.asset];
    const radius = Math.hypot(size[0] * r.sx, size[1] * r.sy, size[2] * r.sz) / 2;
    if (!matrix.elements.every(Number.isFinite) || !Number.isFinite(radius)) throw Error('Invalid instance transform');
    return { source_id: `tidewater:${pin}:debris-scanned:${ordinal}`, source: r,
      matrix_column_major: matrix.elements, quaternion_xyzw: q.toArray(), radius_m: radius };
  });
  save('LICENSE-Tidewater.txt', read('LICENSE'));
  save('CREDITS-scanned.md', read('CREDITS.md'));
  checkSource();
  const descriptor = { schema: 'forge.tidewater.debris-scanned-geometry', version: 1,
    source_commit: pin, source_descriptor_sha256: hash(sourceBytes), source_inputs: {
      glb_parser: inventory.source_inputs['src/world/debris/GLB.js'],
      scanned_contract: inventory.source_inputs['src/world/debris/ScannedDebris.js'] },
    meshes, instances, files, limits: ['Source CPU geometry and transforms only; no rendered qualification.',
      'Atlas pixels/mips, camera LOD/crossfade, material adaptation and Studio editing remain pending.'] };
  fs.mkdirSync(output);
  for (const [name, bytes] of Object.entries(pending)) fs.writeFileSync(path.join(output, name), bytes, { flag: 'wx' });
  fs.writeFileSync(path.join(output, 'scanned-geometry.json'), JSON.stringify(descriptor, null, 2), { flag: 'wx' });
  console.log(JSON.stringify({ meshes: meshes.length, instances: instances.length,
    lod_triangles: [0, 1, 2].map(lod => meshes.filter(m => m.lod === lod).reduce((n, m) => n + m.triangles, 0)),
    payloads: Object.keys(files).length, rendered: false }));
} finally { GPU.device.destroy(); }
