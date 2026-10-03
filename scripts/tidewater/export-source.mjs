// Forge development-only extractor. No dependency from a Forge application.
// Evaluates audited, pinned MIT Tidewater generators in Node; does not edit them.
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { mkdirSync, writeFileSync, readFileSync, lstatSync, existsSync } from 'node:fs';
import { resolve, relative, join, isAbsolute } from 'node:path';
import { pathToFileURL } from 'node:url';
import { exportVegetationMaterials } from './export-vegetation-materials.mjs';
import { exportTerrainMaterial } from './export-terrain-material.mjs';
import { exportVillage } from './export-village.mjs';
import { captureSourceLocalLights } from './capture-source-local-lights.mjs';
import { prepareSourceAppLightFactories } from './source-app-light-factories.mjs';

const PIN = '1438b1abfcaee3267092b75573014f4d9b4a983c';
const [sourceArg, outputArg] = process.argv.slice(2);
if (!sourceArg || !outputArg) throw new Error('Usage: node export-source.mjs SOURCE_CHECKOUT NEW_OUTPUT');
const source = resolve(sourceArg), output = resolve(outputArg);
const rel = relative(source, output);
if (!rel || (!rel.startsWith('..') && !isAbsolute(rel))) throw new Error('Output must be outside source');
if (existsSync(output)) throw new Error('Refusing to overwrite an existing export');
const git = (...args) => execFileSync('git', ['-C', source, ...args], { encoding: 'utf8' }).trim();
if (git('rev-parse', 'HEAD') !== PIN) throw new Error('Unexpected source commit');
if (git('status', '--porcelain=v1', '--untracked-files=all')) throw new Error('Source checkout must be clean');
const hash = data => createHash('sha256').update(data).digest('hex');
const inventory = git('ls-files', '-z').split('\0').filter(Boolean).map(path => {
  const full = join(source, path);
  if (!lstatSync(full).isFile()) throw new Error(`Not a regular source file: ${path}`);
  const bytes = readFileSync(full);
  return { path, bytes: bytes.length, sha256: hash(bytes) };
});
const moduleAt = path => import(pathToFileURL(join(source, path)).href);
// Village assembly uploads fish-prop buffers even for an extraction. Use the
// audited upstream headless adapter rather than stubbing its generation path.
await moduleAt('test/headless.mjs');
const { GPU } = await moduleAt('src/engine/gpu/GPU.js');
await GPU.init({ headless: true });
const [{ TerrainData }, { Village }, { Colliders }, { Scene }, plants, scatter, { WORLD }] = await Promise.all([
  moduleAt('src/world/TerrainData.js'), moduleAt('src/world/Village.js'),
  moduleAt('src/world/Colliders.js'), moduleAt('src/engine/index.js'),
  moduleAt('src/world/vegetation/PlantGeometry.js'), moduleAt('src/world/vegetation/Scatter.js'),
  moduleAt('src/world/WorldLayout.js'),
]);
const terrain = new TerrainData();
const colliders = new Colliders();
const scene = new Scene();
const village = new Village({ scene, terrain, colliders });
const [{ BoatModel }, lighting, { Game }, { Vector3, Color }, { STAND }, { CHANDLERY }, gear] = await Promise.all([
  moduleAt('src/world/BoatModel.js'), moduleAt('src/materials/LocalLights.js'),
  moduleAt('src/game/Game.js'), moduleAt('src/engine/index.js'),
  moduleAt('src/game/FishStand.js'), moduleAt('src/game/Chandlery.js'), moduleAt('src/game/Gear.js'),
]);
const sourceLightBoat = new BoatModel();
sourceLightBoat.group.position.copy(WORLD.boatDock.position);
sourceLightBoat.group.rotation.y = WORLD.boatDock.heading;
const appFactories = prepareSourceAppLightFactories({ sourceCommit: PIN, sourceInputs: inventory,
  appBytes: readFileSync(join(source, 'src/App.js')), gameBytes: readFileSync(join(source, 'src/game/Game.js')),
  Game, Vector3, Color, STAND, CHANDLERY, ...gear });
const localLights = captureSourceLocalLights({
  sourceCommit: PIN, lighting, village, boat: sourceLightBoat, sourceInputs: inventory,
  terrain, appFactories,
  gpu: GPU, captureMode: 'source-export-headless-gpu',
});
const { Vegetation } = await moduleAt('src/world/Vegetation.js');
const vegetation = new Vegetation({ scene, terrain, village });
// Same sampling of obstacle polylines as Vegetation.villageObstacles upstream.
const paths = [village.path, ...(village.sidePaths || [])].filter(p => p?.samples).map(p => {
  const points = p.samples.filter((_, i) => i % 5 === 0).map(s => [s.p.x, s.p.z]);
  const last = p.samples.at(-1); points.push([last.p.x, last.p.z]);
  return { points, width: p.width ?? 1.8 };
});
const footprints = village.getFootprints();
const site = new scatter.VegSite(terrain, { footprints, paths: paths.length ? paths : null });
const records = scatter.scatterVegetation(site);
if (JSON.stringify(records) !== JSON.stringify(vegetation.records)) throw new Error('Application scatter differs');
mkdirSync(output, { recursive: true });
const files = [];
function write(path, bytes) {
  mkdirSync(resolve(output, path, '..'), { recursive: true });
  writeFileSync(join(output, path), bytes, { flag: 'wx' });
  const info = { path, bytes: bytes.length, sha256: hash(bytes) }; files.push(info); return info;
}
const json = (path, value) => write(path, Buffer.from(JSON.stringify(value, null, 2) + '\n'));
function binary(path, array) {
  // Explicit little endian; never expose interleaved buffer padding accidentally.
  const bytes = Buffer.alloc(array.length * array.BYTES_PER_ELEMENT);
  if (array instanceof Float32Array) array.forEach((v, i) => { if (!Number.isFinite(v)) throw new Error(`Nonfinite ${path}`); bytes.writeFloatLE(v, i * 4); });
  else if (array instanceof Uint32Array) array.forEach((v, i) => bytes.writeUInt32LE(v, i * 4));
  else if (array instanceof Uint16Array) array.forEach((v, i) => bytes.writeUInt16LE(v, i * 2));
  else if (array instanceof Uint8Array) bytes.set(array);
  else throw new Error(`Unsupported array ${array.constructor.name}`);
  return { ...write(path, bytes), component_type: array.constructor.name, length: array.length };
}
const terrainMaps = {};
for (const field of ['heights','rock','sand','path','gully','seagrass','rubble','scarp']) {
  terrainMaps[field] = binary(`terrain/${field}.bin`, terrain[field]);
}
const probeCoordinates = [[0,0],[53.6,-77],[40,-118],[-78,58],[-1024,-1024],[1023,1023]];
for (let i = 0; i < 1024; i++) {
  probeCoordinates.push([-1024 + ((i * 1543 + 73) % 2048) + 0.371,
    -1024 + ((i * 997 + 131) % 2048) + 0.619]);
}
json('terrain/metadata.json', { size_m: terrain.size, resolution: terrain.res, origin_m: terrain.origin,
  texel_m: terrain.texel, order: 'row-major-z-x', stage: 'after_village_foundations', maps: terrainMaps,
  pads: terrain.pads, paths: terrain.paths, rock_sites: terrain.rockSites,
  probes: probeCoordinates.map(([x,z]) => ({x,z,height:terrain.heightAt(x,z)})) });
const counts = {};
for (const [species, instances] of Object.entries(records)) {
  if (species === 'villagePalms' && Number.isInteger(instances)) continue;
  if (!Array.isArray(instances)) throw new Error(`Unexpected scatter field: ${species}`);
  const seen = new Set();
  const entries = instances.map(record => {
    const id = hash(Buffer.from(`${PIN}/${species}/${JSON.stringify(record)}`));
    if (seen.has(id)) throw new Error(`Duplicate source instance in ${species}`);
    seen.add(id); return { source_id: id, ...record };
  });
  counts[species] = entries.length;
  json(`vegetation/${species}.json`, entries);
}
const grassMask = scatter.buildGrassMask(site);
binary('vegetation/grass-mask.bin', grassMask.data ?? grassMask.image?.data);
json('vegetation/grass-mask.json', { resolution: grassMask.res, channels: ['dune','meadow','oats','creeper'] });
json('vegetation/obstacles.json', { footprints, paths });
json('village/layout.json', { buildings: village.buildings, footprints, foundation_checks: village.foundationChecks,
  lights: village.lights, pads: terrain.pads });
json('world-layout.json', WORLD);
json('source-local-lights.json', localLights);
json('source-local-lights-acquisition.json', { ...appFactories.receipt,
  descriptor_sha256: hash(Buffer.from(JSON.stringify(localLights, null, 2) + '\n')),
  emitters: localLights.lights.length, registration_sets: localLights.sets,
  phase_reacquisition_reproducible: false, full_emitter_inventory: localLights.full_application_inventory });
const prototypes = {};
for (const name of ['buildPalmNear','buildPalmFar','buildTreeNear','buildShrubNear','buildUnderstory','buildBananas','buildMonsteraMesh','buildBroadleaf']) {
  const result = plants[name](), geometry = result.geometry;
  const attributes = {};
  for (const [key, attr] of Object.entries(geometry.attributes)) {
    const data = new Float32Array(attr.count * attr.itemSize);
    const backing = attr.isInterleavedBufferAttribute ? attr.data.array : attr.array;
    const stride = attr.isInterleavedBufferAttribute ? attr.data.stride : attr.itemSize;
    const offset = attr.isInterleavedBufferAttribute ? attr.offset : 0;
    for (let i = 0; i < attr.count; i++) for (let c = 0; c < attr.itemSize; c++) data[i * attr.itemSize + c] = backing[i * stride + offset + c];
    attributes[key] = { ...binary(`prototypes/${name}/${key}.bin`, data), item_size: attr.itemSize, count: attr.count };
  }
  prototypes[name] = { triangles: result.triangles, attributes, indices: binary(`prototypes/${name}/indices.bin`, new Uint32Array(geometry.index.array)),
    bounds: geometry.boundingBox, shader_deformation_required: true };
}
json('prototypes/catalog.json', { schema: 'forge.tidewater.source-prototypes', version: 1, prototypes,
  lobe_table: plants.LOBE_TABLE, tree_variants: plants.TREE_VARIANTS, shrub_variants: plants.SHRUB_VARIANTS,
  understory_kinds: plants.UNDERSTORY, broadleaf_kinds: plants.BROADLEAF });
await exportVegetationMaterials({moduleAt,json,write,binary,GPU,vegetation});
await exportTerrainMaterial({moduleAt,json,write,binary,GPU,terrain});
await exportVillage({moduleAt,json,write,binary,GPU,village,hash});
json('source-inventory.json', inventory);
write('LICENSE-Tidewater.txt', readFileSync(join(source, 'LICENSE')));
write('CREDITS-Tidewater.md', readFileSync(join(source, 'CREDITS.md')));
json('manifest.json', { schema: 'forge.tidewater.source-export', version: 1,
  capture_mode: 'source-export-headless-gpu', gpu_initialized: GPU.device !== null, browser_required: false,
  source: { url: 'https://github.com/dgreenheck/tidewater', commit: PIN },
  extractor: { node: process.version, sha256: hash(readFileSync(new URL(import.meta.url))),
    vegetation_materials_sha256: hash(readFileSync(new URL('./export-vegetation-materials.mjs',import.meta.url))),
    terrain_material_sha256: hash(readFileSync(new URL('./export-terrain-material.mjs',import.meta.url))),
    village_sha256: hash(readFileSync(new URL('./export-village.mjs',import.meta.url))),
    local_lights_sha256: hash(readFileSync(new URL('./capture-source-local-lights.mjs',import.meta.url))),
    app_light_factories_sha256: hash(readFileSync(new URL('./source-app-light-factories.mjs',import.meta.url))),
    rgba_readback_sha256: hash(readFileSync(new URL('./read-rgba-mips.mjs',import.meta.url))) },
  status: 'partial_source_extraction_not_forge_import', vegetation_counts: counts, files,
  missing: ['village_individual_meshes','village_fish_props','village_dynamic_animation','rock_instances','debris','reef','fauna','boat','gameplay','render_effects'],
  renderer_parity: false, studio_qualified: false });
if (git('status', '--porcelain=v1', '--untracked-files=all')) throw new Error('Source changed during export');
console.log(JSON.stringify({ output, vegetation_counts: counts, files: files.length, status: 'partial_source_extraction' }));
GPU.device.destroy();
process.exit(0);
