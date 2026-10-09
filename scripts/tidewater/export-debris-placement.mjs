// SPDX-License-Identifier: MIT
// Tidewater source copyright 2026 DRG Software Solutions LLC, commit 1438b1ab.
// Inventory the audited pinned generators; do not edit upstream or fetch scanned assets.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { pathToFileURL } from 'node:url';

const [sourceArg, outputArg, mode, ownershipMode, collisionMode] = process.argv.slice(2);
if (mode !== undefined && mode !== '--retain-payloads') throw Error('Unknown acquisition mode');
if (ownershipMode !== undefined && (mode !== '--retain-payloads' || ownershipMode !== '--observe-ownership'))
  throw Error('Ownership observation requires retained payloads');
if (collisionMode !== undefined && (ownershipMode !== '--observe-ownership' || collisionMode !== '--observe-collisions'))
  throw Error('Collision observation requires ownership observation');
if (!sourceArg || !outputArg) throw Error('SOURCE NEW_EXTERNAL_DIRECTORY required');
const source = path.resolve(sourceArg), output = path.resolve(outputArg);
const pin = '1438b1abfcaee3267092b75573014f4d9b4a983c';
const git = (...args) => execFileSync('git', ['-c', `safe.directory=${source.replaceAll('\\', '/')}`,
  '-C', source, ...args], { encoding: 'utf8' }).trim();
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
if (git('rev-parse', 'HEAD') !== pin || git('status', '--porcelain', '--untracked-files=all'))
  throw Error('Clean pinned source required');
const relative = path.relative(source, output);
if (fs.existsSync(output) || !relative || (!relative.startsWith('..') && !path.isAbsolute(relative)))
  throw Error('Fresh external output required');
const inputs = Object.fromEntries(git('ls-files', '-z').split('\0').filter(Boolean).map(name => {
  const file = path.join(source, name);
  if (!fs.lstatSync(file).isFile()) throw Error('Regular tracked input required: ' + name);
  return [name, hash(fs.readFileSync(file))];
}));
const at = name => import(pathToFileURL(path.join(source, name)).href);
await at('test/headless.mjs');
const { GPU } = await at('src/engine/gpu/GPU.js');
await GPU.init({ headless: true });
const [{ Scene }, { TerrainData }, { Colliders }, { Village }, { Vegetation }, { Rocks },
  { Builder }, { InstancedProps }, { DebrisPlacer }] = await Promise.all([
  at('src/engine/index.js'), at('src/world/TerrainData.js'), at('src/world/Colliders.js'),
  at('src/world/Village.js'), at('src/world/Vegetation.js'), at('src/world/Rocks.js'),
  at('src/world/village/GeoBuilder.js'), at('src/world/Props.js'),
  at('src/world/debris/DebrisPlacement.js'),
]);
const scene = new Scene(), terrain = new TerrainData(), colliders = new Colliders();
const village = new Village({ scene, terrain, colliders });
const vegetation = new Vegetation({ scene, terrain, village });
// Rocks placement and colliders use TerrainData; shader uploads do not alter placement.
const rocks = new Rocks({ scene, terrain, village, colliders });
const colliderStart = { boxes: colliders.boxes.length, cylinders: colliders.cylinders.length };
const B = new Builder(), inst = new InstancedProps(B);
let PlacementType = DebrisPlacer, observedModule = null, observedModuleHash = null;
if (ownershipMode) {
  const original = fs.readFileSync(path.join(source, 'src/world/debris/DebrisPlacement.js'), 'utf8');
  const marker = 'stone( B, g, jx, jz,';
  if (original.split(marker).length !== 2) throw Error('Pinned cove emission marker changed');
  let code = original.replace(marker, 'observedCoveStone( B, g, jx, jz,');
  code = code.replace(/from\s+(['"])(\.[^'"]+)\1/g, (_, quote, relativePath) =>
    `from ${JSON.stringify(pathToFileURL(path.resolve(source, 'src/world/debris', relativePath)).href)}`);
  code += `\nlet coveObserver;\nexport function setCoveObserver(fn) { coveObserver = fn; }\n` +
    `function observedCoveStone(...args) { const result = stone(...args); coveObserver(args[2], args[3]); return result; }\n`;
  observedModuleHash = hash(Buffer.from(code));
  observedModule = await import('data:text/javascript;base64,' + Buffer.from(code).toString('base64'));
  PlacementType = observedModule.DebrisPlacer;
}
const placer = new PlacementType({ B, inst, terrain, village, vegetation, rocks, colliders, seed: 5150 });
const ownership = [], cursor = {};
const collisionEvents = [], pendingCollisions = [], scanReturns = new WeakMap();
let logCollisionOwner = null;
const captureOwner = (kind, x, z, sourceCount, basis = 'source-count-emission-boundary') => {
  const parts = {};
  const min = [Infinity, Infinity, Infinity], max = [-Infinity, -Infinity, -Infinity];
  for (const [key, b] of Object.entries(B.batches)) {
    const start = cursor[key] ?? { vertices: 0, indices: 0 };
    const vertices = b.vcount - start.vertices, indices = b.idx.length - start.indices;
    if (!vertices && !indices) continue;
    if (vertices <= 0 || indices <= 0 || indices % 3) throw Error('Incomplete owner: ' + key);
    for (let i = start.indices; i < b.idx.length; i++)
      if (b.idx[i] < start.vertices || b.idx[i] >= b.vcount) throw Error('Cross-owner index');
    for (let i = start.vertices; i < b.vcount; i++) for (let axis = 0; axis < 3; axis++) {
      const value = b.pos[i * 3 + axis];
      min[axis] = Math.min(min[axis], value); max[axis] = Math.max(max[axis], value);
    }
    parts[key] = { vertex_start: start.vertices, vertices, index_start: start.indices, indices };
    cursor[key] = { vertices: b.vcount, indices: b.idx.length };
  }
  if (Object.keys(parts).length) {
    const owner = {
      source_id: `tidewater:${pin}:debris-cpu-owner:${ownership.length}`, kind, source_count: sourceCount,
      source_anchor_xz: x === null ? null : [x, z], bounds_world: { min, max }, parts,
      ownership_basis: basis,
    };
    ownership.push(owner);
    if (collisionMode) for (const event of pendingCollisions.splice(0)) event.owner_source_id = owner.source_id;
  } else if (collisionMode && pendingCollisions.length) {
    throw Error('Collision emission has no procedural owner: ' + kind);
  }
};
if (collisionMode) {
  // Preserve the exact returned object linking _scanLog to _logCollider. No spatial
  // matching, random calls, source writes, or changes to the original method bodies.
  const scanLog = placer._scanLog;
  placer._scanLog = function (...args) {
    const before = this.scanned.length, value = scanLog.apply(this, args);
    if (this.scanned.length !== before + 1 || !value || typeof value !== 'object')
      throw Error('Scanned emitter return contract changed');
    scanReturns.set(value, `tidewater:${pin}:debris-scanned:${before}`);
    return value;
  };
  const logCollider = placer._logCollider;
  placer._logCollider = function (...args) {
    if (logCollisionOwner !== null) throw Error('Nested log collider emission');
    logCollisionOwner = scanReturns.get(args[4]);
    if (!logCollisionOwner) throw Error('Log collision lost its source return identity');
    try { return logCollider.apply(this, args); } finally { logCollisionOwner = null; }
  };
  for (const [method, collection, shape] of [['addBox', 'boxes', 'box'], ['addCyl', 'cylinders', 'cylinder']]) {
    const original = placer[method];
    placer[method] = function (...args) {
      const before = colliders[collection].length, value = original.apply(this, args);
      if (colliders[collection].length !== before + 1) throw Error('Collision emitter contract changed');
      const event = { shape, index: before - colliderStart[collection], owner_source_id: logCollisionOwner,
        basis: logCollisionOwner ? 'source-scan-return-object-to-log-collider' : 'source-count-emission-boundary' };
      collisionEvents.push(event);
      if (!logCollisionOwner) pendingCollisions.push(event);
      return value;
    };
  }
}
if (ownershipMode) {
  observedModule.setCoveObserver((x, z) => captureOwner('coveStone', x, z, 1, 'source-stone-emitter-boundary'));
  // Instance-local observer; the source implementation and its random stream are
  // untouched. Count is called after accepted item/group emission, never per vertex.
  const count = placer.count;
  placer.count = function (kind, n = 1, x = null, z = null) {
    const value = count.call(this, kind, n, x, z);
    captureOwner(kind, x, z, n);
    return value;
  };
}
placer.run();
if (ownershipMode) {
  captureOwner('unattributed-tail', null, null, null);
  if (ownership.some(o => o.kind === 'unattributed-tail')) throw Error('Unattributed geometry remains');
  if (collisionMode && (pendingCollisions.length || collisionEvents.length !== placer.newColliders))
    throw Error('Unattributed or missing collision events');
}
const batches = Object.fromEntries(Object.entries(B.batches).map(([name, batch]) => [name, {
  vertices: batch.vcount, indices: batch.idx.length,
  attributes: Object.fromEntries(['pos', 'nrm', 'uv', 'tint', 'data'].map(key => [key,
    hash(Buffer.from(new Float64Array(batch[key]).buffer))])),
  indices_sha256: hash(Buffer.from(new Uint32Array(batch.idx).buffer)),
}]));
if (!Object.keys(batches).length || !placer.log.length) throw Error('Actual placement inventory absent');
if (!Object.entries(inputs).every(([name, digest]) => hash(fs.readFileSync(path.join(source, name))) === digest)
    || git('status', '--porcelain', '--untracked-files=all')) throw Error('Source changed during observation');
fs.mkdirSync(output, { recursive: true });
if (mode === '--retain-payloads') {
  const { Debris } = await at('src/world/Debris.js');
  const payloads = {};
  const save = (name, bytes, description) => {
    fs.writeFileSync(path.join(output, name), bytes);
    payloads[name] = { bytes: bytes.length, sha256: hash(bytes), ...description };
  };
  const widths = { pos: 3, nrm: 3, uv: 2, tint: 3, data: 4 };
  for (const [name, batch] of Object.entries(B.batches)) {
    if (batch.idx.length % 3 || batch.idx.some(i => !Number.isInteger(i) || i < 0 || i >= batch.vcount))
      throw Error('Invalid triangle indices: ' + name);
    for (const [attribute, width] of Object.entries(widths)) {
      const values = batch[attribute];
      if (values.length !== batch.vcount * width || values.some(v => !Number.isFinite(v)))
        throw Error('Invalid attribute: ' + name + '/' + attribute);
      save(`${name}.${attribute}.f64.bin`, Buffer.from(new Float64Array(values).buffer),
        { scalar: 'float64', components: width, vertices: batch.vcount, stage: 'before-contact' });
    }
    save(`${name}.indices.u32.bin`, Buffer.from(new Uint32Array(batch.idx).buffer),
      { scalar: 'uint32', indices: batch.idx.length, stage: 'before-assembly' });
    // The exact audited contact function only mutates tint. Preserve raw batches for
    // ownership reconstruction and do not change the subsequent inventory digests.
    const contact = { ...batch, tint: batch.tint.slice(), vcount: batch.vcount };
    Debris.prototype._bakeContact.call({ terrainData: terrain }, contact);
    save(`${name}.contact-tint.f64.bin`, Buffer.from(new Float64Array(contact.tint).buffer),
      { scalar: 'float64', components: 3, vertices: batch.vcount, stage: 'after-contact' });
  }
  if (!(placer.mask.data instanceof Uint8Array) || placer.mask.data.length !== placer.mask.res ** 2 * 4)
    throw Error('Invalid actual pebble density mask');
  save('pebble-mask.rgba8.bin', Buffer.from(placer.mask.data),
    { scalar: 'uint8', components: 4, resolution: placer.mask.res, texel_m: placer.mask.texel,
      origin_m: terrain.origin });
  const addedColliders = { boxes: colliders.boxes.slice(colliderStart.boxes),
    cylinders: colliders.cylinders.slice(colliderStart.cylinders) };
  save('colliders.json', Buffer.from(JSON.stringify(addedColliders, null, 2)),
    { boxes: addedColliders.boxes.length, cylinders: addedColliders.cylinders.length });
  if (collisionMode) {
    const owners = [...ownership.map(owner => ({ source_id: owner.source_id, category: 'procedural' })),
      ...placer.scanned.map((_, i) => ({ source_id: `tidewater:${pin}:debris-scanned:${i}`, category: 'scanned' }))];
    const known = new Set(owners.map(owner => owner.source_id));
    if (collisionEvents.some(event => !known.has(event.owner_source_id))) throw Error('Unknown collision owner');
    for (const owner of owners) owner.collisions = collisionEvents.filter(event => event.owner_source_id === owner.source_id);
    fs.writeFileSync(path.join(output, 'collision-ownership.json'), JSON.stringify({
      schema: 'forge.tidewater.debris-collision-ownership', version: 1, source_commit: pin,
      colliders_sha256: payloads['colliders.json'].sha256, owners,
      events: collisionEvents,
      limits: ['Source emission ownership only; portable collider frames and Rapier registration remain unqualified.',
        'Empty owner populations mean no collision emitted by the observed source run, not a gameplay waiver.'],
    }, null, 2));
  }
  // Endianness is explicit; a reader must not assume a host-independent typed array.
  const littleEndian = new Uint8Array(new Uint32Array([1]).buffer)[0] === 1;
  if (!littleEndian) throw Error('Acquisition requires little-endian binary output');
  fs.writeFileSync(path.join(output, 'payload-manifest.json'), JSON.stringify({
    schema: 'forge.tidewater.debris-source-payloads', version: 1, source_commit: pin,
    byte_order: 'little-endian', payloads, final_assembly: false, ownership_partitioned: false,
    scanned_models_loaded: false, materials_ported: false, rendered: false,
  }, null, 2));
  if (ownershipMode) fs.writeFileSync(path.join(output, 'ownership.json'), JSON.stringify({
    schema: 'forge.tidewater.debris-source-ownership', version: 1, source_commit: pin,
    observed_module_sha256: observedModuleHash,
    owners: ownership, scanned_instances: placer.scanned.map((instance, i) => ({
      source_id: `tidewater:${pin}:debris-scanned:${i}`, ...instance,
    })),
    limits: ['Emission boundary owners include intentional compound groups.',
      'Cove stones are observed individually at the exact source emitter boundary.',
      'Collision attachment, portable pivots and runtime authoring integration are not qualified.'],
  }, null, 2));
}
if (!Object.entries(inputs).every(([name, digest]) => hash(fs.readFileSync(path.join(source, name))) === digest)
    || git('status', '--porcelain', '--untracked-files=all')) throw Error('Source changed during acquisition');
const result = { schema: 'forge.tidewater.debris-placement-inventory', version: 1,
  source_commit: pin, source_inputs: inputs, seed: 5150, counts: placer.counts,
  groups: placer.log, scanned_instances: placer.scanned, batches,
  new_colliders: placer.newColliders, rocks_instances: rocks.instances.length,
  source_unchanged: true, geometry_packaged: false, materials_acquired: false,
  scene_rendered: false, visual_parity: false,
  limits: ['CPU placement observation before contact baking and material assembly.',
    'TerrainGPU shaders, PebbleField and scanned GLB loading are not executed.'] };
fs.writeFileSync(path.join(output, 'inventory.json'), JSON.stringify(result, null, 2));
fs.copyFileSync(path.join(source, 'LICENSE'), path.join(output, 'LICENSE-Tidewater.txt'));
fs.copyFileSync(path.join(source, 'public/models/debris/CREDITS.md'), path.join(output, 'CREDITS-scanned.md'));
console.log(JSON.stringify({ groups: placer.log.length, scanned: placer.scanned.length,
  batches: Object.keys(batches), counts: placer.counts, source_unchanged: true,
  visual_parity: false }));
GPU.device.destroy();
