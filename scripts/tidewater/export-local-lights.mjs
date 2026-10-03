// Development-only, pinned source acquisition; no Forge runtime dependency.
// Captures the actual source producers and closes their original code/notice.
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { existsSync, lstatSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { isAbsolute, join, relative, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import assert from 'node:assert/strict';
import { captureSourceLocalLights } from './capture-source-local-lights.mjs';
import { prepareSourceAppLightFactories } from './source-app-light-factories.mjs';
import { observeVillageLightOwners } from './source-village-light-owners.mjs';
import { prepareBuildingOwnerObservation, buildingLightOwnerMap,
  villageConstructionDigest } from './source-building-light-owners.mjs';
import { sideLightOwnerMap } from './source-village-side-light-owners.mjs';
import { acquireVendorOwnerFrames } from './source-vendor-light-owners.mjs';

const PIN = '1438b1abfcaee3267092b75573014f4d9b4a983c';
const [sourceArg, outputArg] = process.argv.slice(2);
if (!sourceArg || !outputArg) throw new Error('Usage: node export-local-lights.mjs SOURCE_CHECKOUT NEW_OUTPUT');
const source = resolve(sourceArg), output = resolve(outputArg), rel = relative(source, output);
if (!rel || (!rel.startsWith('..') && !isAbsolute(rel)) || existsSync(output)) {
  throw new Error('Acquisition requires a new directory outside the canonical checkout');
}
const git = (...args) => execFileSync('git', ['-C', source, ...args], { encoding: 'utf8' }).trim();
if (git('rev-parse', 'HEAD') !== PIN || git('status', '--porcelain=v1', '--untracked-files=all')) {
  throw new Error('Source revision must be pinned and clean');
}
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
// Original construction code and the development adapter are receipt inputs.
// Public models/audio and npm binaries are not loaded by this bounded capture
// and are not relabelled as redistributable sources in its package.
const paths = git('ls-files', '-z').split('\0').filter(path =>
  path.startsWith('src/') || ['LICENSE', 'test/headless.mjs', 'package.json', 'package-lock.json'].includes(path));
const originals = new Map();
const inventory = paths.map(path => {
  if (path.includes('\\') || path.split('/').some(part => !part || part === '.' || part === '..')) {
    throw new Error('Unsafe source path');
  }
  const full = join(source, path);
  if (!lstatSync(full).isFile()) throw new Error('Source input is not a regular file: ' + path);
  const bytes = readFileSync(full);
  originals.set(path, bytes);
  return { path, bytes: bytes.length, sha256: hash(bytes) };
});
const moduleAt = path => import(pathToFileURL(join(source, path)).href);
// Audited upstream headless SDK: Village._assemble uploads fish-prop buffers.
await moduleAt('test/headless.mjs');
const { GPU } = await moduleAt('src/engine/gpu/GPU.js');
await GPU.init({ headless: true });
try {
  const [{ TerrainData }, { Village }, { Colliders }, engine, { WORLD }, { BoatModel },
    lighting, { Game }, { STAND }, { CHANDLERY }, gear] = await Promise.all([
    moduleAt('src/world/TerrainData.js'), moduleAt('src/world/Village.js'),
    moduleAt('src/world/Colliders.js'), moduleAt('src/engine/index.js'),
    moduleAt('src/world/WorldLayout.js'), moduleAt('src/world/BoatModel.js'),
    moduleAt('src/materials/LocalLights.js'), moduleAt('src/game/Game.js'),
    moduleAt('src/game/FishStand.js'), moduleAt('src/game/Chandlery.js'), moduleAt('src/game/Gear.js'),
  ]);
  const buildVillage = Type => {
    const terrain = new TerrainData(), scene = new engine.Scene(), colliders = new Colliders();
    return { terrain, colliders, village: new Type({ scene, terrain, colliders }) };
  };
  const baseline = buildVillage(Village);
  const observation = await prepareBuildingOwnerObservation({ sourceRoot: source,
    sourceCommit: PIN, villageBytes: originals.get('src/world/Village.js'),
    expectedVillageHash: inventory.find(row => row.path === 'src/world/Village.js').sha256,
    includeSideOwners: true });
  const observed = buildVillage(observation.Village);
  assert.throws(() => buildVillage(observation.Village), /permits only one Village construction/);
  const baselineDigest = villageConstructionDigest(baseline);
  if (villageConstructionDigest(observed) !== baselineDigest) {
    throw new Error('Building owner observation changed geometry, terrain, lights or colliders');
  }
  const { terrain, village } = observed, boat = new BoatModel();
  const extraVillageOwners = buildingLightOwnerMap(observation.observations, village);
  for (const [light, owner] of sideLightOwnerMap(observation.sideObservations, village)) {
    assert(!extraVillageOwners.has(light), 'A Village light has two captured owners');
    extraVillageOwners.set(light, owner);
  }
  boat.group.position.copy(WORLD.boatDock.position);
  boat.group.rotation.y = WORLD.boatDock.heading;
  const appFactories = prepareSourceAppLightFactories({ sourceCommit: PIN, sourceInputs: inventory,
    appBytes: originals.get('src/App.js'), gameBytes: originals.get('src/game/Game.js'),
    Game, Vector3: engine.Vector3, Color: engine.Color, STAND, CHANDLERY, ...gear });
  const vendorParents = acquireVendorOwnerFrames({ sourceCommit: PIN, sourceInputs: inventory,
    originals, Group: engine.Group, terrain, STAND, CHANDLERY });
  const descriptor = captureSourceLocalLights({ sourceCommit: PIN, lighting, village, boat,
    terrain, appFactories, sourceInputs: inventory, gpu: GPU, captureMode: 'source-export-headless-gpu',
    extraVillageOwners, vendorOwners: vendorParents.frames });
  const owners = observeVillageLightOwners(village);
  const ownersByFrame = new Map([...owners.values(), ...extraVillageOwners.values(), ...vendorParents.frames]
    .map(owner => [owner.id, owner]));
  const editableBindings = descriptor.lights.filter(light => light.owner_frame !== 'world')
    .map(light => ({ light_id: light.id, owner_frame: light.owner_frame,
      source_binding: light.owner_frame === 'boat-root'
        ? { producer_path: 'src/world/BoatModel.js', collection: null, ordinal: null,
          anchor: 'group', evidence: 'source-object-parent-frame' }
        : ownersByFrame.get(light.owner_frame).binding }));
  if (git('status', '--porcelain=v1', '--untracked-files=all')) throw new Error('Source changed during acquisition');
  // Verify the bytes that construction actually read have not drifted either.
  for (const pin of inventory) {
    const bytes = readFileSync(join(source, pin.path));
    if (bytes.length !== pin.bytes || hash(bytes) !== pin.sha256) throw new Error('Source input changed: ' + pin.path);
  }
  const files = {};
  function write(file, bytes) {
    mkdirSync(resolve(output, file, '..'), { recursive: true });
    writeFileSync(join(output, file), bytes, { flag: 'wx' });
    const reference = { file, bytes: bytes.length, sha256: hash(bytes) };
    files[file] = { bytes: reference.bytes, sha256: reference.sha256 };
    return reference;
  }
  const json = (file, value) => write(file, Buffer.from(JSON.stringify(value, null, 2) + '\n'));
  const sources = Object.fromEntries(inventory.map(pin => [pin.path, write('source/' + pin.path, originals.get(pin.path))]));
  const data = json('source-local-lights.json', descriptor);
  const acquisition = json('acquisition.json', { ...appFactories.receipt,
    recorded_utc: new Date().toISOString(), capture_mode: 'source-export-headless-gpu',
    gpu_initialized: true, browser_required: false, descriptor: data,
    emitters: descriptor.lights.length, registration_sets: descriptor.sets,
    full_emitter_inventory: descriptor.full_application_inventory,
    phase_reacquisition_reproducible: false,
    extractor_sha256: hash(readFileSync(new URL(import.meta.url))),
    local_lights_sha256: hash(readFileSync(new URL('./capture-source-local-lights.mjs', import.meta.url))),
    app_light_factories_sha256: hash(readFileSync(new URL('./source-app-light-factories.mjs', import.meta.url))),
    village_light_owners_sha256: hash(readFileSync(new URL('./source-village-light-owners.mjs', import.meta.url))),
    building_light_owners_sha256: hash(readFileSync(new URL('./source-building-light-owners.mjs', import.meta.url))),
    side_light_owners_sha256: hash(readFileSync(new URL('./source-village-side-light-owners.mjs', import.meta.url))),
    vendor_light_owners_sha256: hash(readFileSync(new URL('./source-vendor-light-owners.mjs', import.meta.url))),
    vendor_owner_observation: { ...vendorParents.receipt,
      frames: vendorParents.frames.map(row => ({ id: row.id, world_from_local: [...row.matrix.elements] })) },
    side_owner_observation: { schema: 'forge.source-village-side-light-owner-observation', version: 1,
      emitted_lights: observation.sideObservations.reduce((sum, row) => sum + (row.lights?.length ?? 1), 0),
      records: observation.sideObservations.map(row => ({ producer: row.producer,
        collection: row.collection, ordinal: row.ordinal, world_from_local: [...row.frame.elements],
        source_arguments: row.source_arguments, emitted_lights: row.lights?.length ?? 1 })) },
    building_owner_observation: { ...observation.receipt,
      source_scene_digest: baselineDigest, unchanged_geometry_terrain_lights_colliders: true,
      second_construction_refused_before_generator: true,
      building_invocations: observation.observations.map((row, ordinal) => ({
        ordinal, producer: row.producer, source_spec: row.source_spec,
        source_object_id: `tidewater:${PIN}:building:${ordinal}`,
        anchor: 'construction-root',
        world_from_local: [...row.frame.elements], root_scopes: row.root_scopes,
        emitted_lights: row.lights.length })) },
    editable_ownership: { schema: 'forge.source-local-light-owner-observation', version: 1,
      resolved: editableBindings,
      unresolved_light_ids: descriptor.lights.filter(light => light.owner_frame === 'world').map(light => light.id),
      source_binding_complete: editableBindings.length === descriptor.lights.length,
      catalogue_binding_executed: false, studio_projection_executed: false },
  });
  const license = sources.LICENSE;
  if (!license) throw new Error('Original source license is absent');
  json('manifest.json', { schema: 'forge.source-local-lights.package', version: 1,
    source_commit: PIN, descriptor: data, acquisition, license, sources, files,
    status: 'source-local-light-acquisition-not-runtime-import', renderer_parity: false, studio_qualified: false });
  console.log(JSON.stringify({ output, emitters: descriptor.lights.length,
    registration_sets: descriptor.sets, descriptor_sha256: data.sha256,
    manifest_sha256: hash(readFileSync(join(output, 'manifest.json'))),
    source_files: inventory.length, renderer_parity: false, studio_qualified: false }));
} finally {
  GPU.device.destroy();
}
