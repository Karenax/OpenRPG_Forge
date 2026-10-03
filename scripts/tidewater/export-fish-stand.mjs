/** Export the pinned Tidewater FishStand mesh and its source material inputs.
 *
 * This creates a source-derived technical acquisition, not a GLB or a Bevy-ready
 * material. The derived module lives only in the new output directory; the
 * upstream checkout is opened read-only.
 */
import { copyFileSync, cpSync, existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { basename, dirname, relative, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const COMMIT = '1438b1abfcaee3267092b75573014f4d9b4a983c';
const sha256 = data => createHash('sha256').update(data).digest('hex');
const bytes = path => readFileSync(path);
const record = path => ({ bytes: bytes(path).length, sha256: sha256(bytes(path)) });
const die = message => { throw new Error(message); };

function files(root) {
  const out = [];
  const visit = path => {
    for (const entry of readdirSync(path, { withFileTypes: true })) {
      const child = resolve(path, entry.name);
      if (entry.isDirectory()) visit(child);
      else if (entry.isFile()) out.push(child);
      else die(`unsupported source item: ${child}`);
    }
  };
  visit(root);
  return out.sort();
}

async function exportMesh(sourceRoot, output) {
  const fishStand = resolve(sourceRoot, 'src/game/FishStand.js');
  const stallKit = resolve(sourceRoot, 'src/game/StallKit.js');
  const props = resolve(sourceRoot, 'public/models/props');
  const propsInfo = resolve(props, 'props.json');
  const propsBin = resolve(props, 'props.bin');
  for (const path of [fishStand, stallKit, propsInfo, propsBin, resolve(sourceRoot, 'LICENSE'), resolve(props, 'CREDITS.md')]) {
    if (!existsSync(path)) die(`required source missing: ${path}`);
  }
  const head = execFileSync('git', ['-C', sourceRoot, 'rev-parse', 'HEAD'], { encoding: 'utf8' }).trim();
  if (head !== COMMIT) die(`source HEAD differs: ${head}`);
  const changed = execFileSync('git', [
    '-C', sourceRoot, 'status', '--porcelain', '--untracked-files=no', '--',
    'src/game/FishStand.js', 'src/game/StallKit.js', 'public/models/props', 'LICENSE',
  ], { encoding: 'utf8' }).trim();
  if (changed) die(`tracked FishStand input differs:\n${changed}`);
  if (existsSync(output)) die(`output already exists: ${output}`);
  mkdirSync(output, { recursive: true });
  const sourceCopy = resolve(output, 'source');
  mkdirSync(sourceCopy, { recursive: true });
  copyFileSync(fishStand, resolve(sourceCopy, 'FishStand.js'));
  copyFileSync(stallKit, resolve(sourceCopy, 'StallKit.js'));
  copyFileSync(resolve(sourceRoot, 'LICENSE'), resolve(sourceCopy, 'LICENSE-Tidewater.txt'));
  cpSync(props, resolve(sourceCopy, 'props'), { recursive: true, dereference: false, errorOnExist: true });

  // Only make the private source builder callable; imports point back to the clean checkout.
  let moduleText = readFileSync(fishStand, 'utf8');
  const replacements = new Map([
    ['../engine/index.js', resolve(sourceRoot, 'src/engine/index.js')],
    ['../engine/geometry/BufferGeometryUtils.js', resolve(sourceRoot, 'src/engine/geometry/BufferGeometryUtils.js')],
    ['../world/boat/GeoKit.js', resolve(sourceRoot, 'src/world/boat/GeoKit.js')],
    ['./GameMaterials.js', resolve(sourceRoot, 'src/game/GameMaterials.js')],
    ['./Vendor.js', resolve(sourceRoot, 'src/game/Vendor.js')],
    ['./StallKit.js', resolve(sourceRoot, 'src/game/StallKit.js')],
    ['../world/fish/FishProps.js', resolve(sourceRoot, 'src/world/fish/FishProps.js')],
    ['./FishTable.js', resolve(sourceRoot, 'src/game/FishTable.js')],
  ]);
  for (const [from, to] of replacements) moduleText = moduleText.replaceAll(`'${from}'`, `'${pathToFileURL(to).href}'`);
  if (!moduleText.includes('function buildStallKit( assets )')) die('FishStand source signature changed');
  moduleText = moduleText.replace('function buildStallKit( assets )', 'export function buildStallKit( assets )');
  const derived = resolve(output, '.derived-fish-stand.mjs');
  writeFileSync(derived, moduleText, 'utf8');

  const { buildStallKit } = await import(`${pathToFileURL(derived).href}?sha=${sha256(Buffer.from(moduleText))}`);
  const info = JSON.parse(readFileSync(propsInfo, 'utf8'));
  const raw = bytes(propsBin);
  const assets = {
    info,
    verts: new Float32Array(raw.buffer, raw.byteOffset, info.vertexCount * info.vertexFloats),
    index: new Uint32Array(raw.buffer, raw.byteOffset + info.indexByteOffset, info.indexCount),
  };
  const mesh = buildStallKit(assets);
  const geometry = resolve(output, 'geometry');
  mkdirSync(geometry);
  const expected = {
    position: 3, normal: 3, uv: 2, color: 4, aLayer: 1, aUV2: 2,
  };
  const attributes = {};
  for (const [name, itemSize] of Object.entries(expected)) {
    const attribute = mesh.attributes[name];
    if (!attribute || attribute.itemSize !== itemSize || !ArrayBuffer.isView(attribute.array)) die(`invalid mesh attribute: ${name}`);
    const target = resolve(geometry, `${name}.f32le.bin`);
    const data = Buffer.from(attribute.array.buffer, attribute.array.byteOffset, attribute.array.byteLength);
    writeFileSync(target, data);
    attributes[name] = { file: basename(target), item_size: itemSize, count: attribute.count, ...record(target) };
  }
  if (!mesh.index || !ArrayBuffer.isView(mesh.index.array)) die('FishStand index missing');
  const indexTarget = resolve(geometry, 'indices.u32le.bin');
  writeFileSync(indexTarget, Buffer.from(mesh.index.array.buffer, mesh.index.array.byteOffset, mesh.index.array.byteLength));
  const indices = { file: basename(indexTarget), item_size: 4, count: mesh.index.count, ...record(indexTarget) };
  if (!Number.isInteger(indices.count / 3)) die('FishStand triangle stream malformed');

  const sourceFiles = {};
  for (const path of files(sourceCopy)) sourceFiles[relative(output, path).replaceAll('\\', '/')] = record(path);
  const manifest = {
    schema: 'forge.tidewater.fish-stand-source-export', version: 1,
    source: { repository: 'https://github.com/dgreenheck/tidewater', commit: COMMIT },
    asset: { id: 'tidewater:fish-stand:joe', source_name: 'FishStandStall', placement: { x: 49.9, z: -74.6, yaw_radians: 1.45, y: 'terrain.heightAt(x,z)' } },
    geometry: { attributes, indices, triangles: indices.count / 3 },
    source_files: sourceFiles,
    material: {
      source: 'source/StallKit.js', inputs: 'source/props/',
      status: 'source-acquired-not-transposed',
      limitation: 'Custom Tidewater WGSL texture-array material with aLayer/aUV2; geometry export is not visual-equivalence proof for Bevy.',
    },
    licenses: {
      tidewater_code: { file: 'source/LICENSE-Tidewater.txt', spdx: 'MIT', ...record(resolve(sourceCopy, 'LICENSE-Tidewater.txt')) },
      props: { file: 'source/props/CREDITS.md', declared_license: 'CC0-1.0', ...record(resolve(sourceCopy, 'props/CREDITS.md')) },
    },
  };
  writeFileSync(resolve(output, 'manifest.json'), `${JSON.stringify(manifest, null, 2)}\n`, 'utf8');
  rmSync(derived);
  return manifest;
}

function verify(output) {
  const manifestPath = resolve(output, 'manifest.json');
  const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
  if (manifest.schema !== 'forge.tidewater.fish-stand-source-export' || manifest.version !== 1 || manifest.source.commit !== COMMIT) die('unknown export manifest');
  if (manifest.material.status !== 'source-acquired-not-transposed') die('material status changed');
  let vertices = null;
  for (const [name, descriptor] of Object.entries(manifest.geometry.attributes)) {
    const raw = bytes(resolve(output, 'geometry', descriptor.file));
    if (raw.length !== descriptor.bytes || sha256(raw) !== descriptor.sha256 || raw.length !== descriptor.count * descriptor.item_size * 4) die(`invalid attribute: ${name}`);
    const values = new Float32Array(raw.buffer, raw.byteOffset, raw.byteLength / 4);
    if (values.some(value => !Number.isFinite(value))) die(`non-finite attribute: ${name}`);
    if (name === 'position') vertices = descriptor.count;
  }
  const index = manifest.geometry.indices;
  const rawIndex = bytes(resolve(output, 'geometry', index.file));
  if (rawIndex.length !== index.bytes || sha256(rawIndex) !== index.sha256 || rawIndex.length !== index.count * 4 || index.count % 3) die('invalid index stream');
  const indices = new Uint32Array(rawIndex.buffer, rawIndex.byteOffset, rawIndex.byteLength / 4);
  if (!vertices || indices.some(value => value >= vertices)) die('index out of bounds');
  for (const [name, expected] of Object.entries(manifest.source_files)) {
    const actual = record(resolve(output, name));
    if (actual.bytes !== expected.bytes || actual.sha256 !== expected.sha256) die(`source input drift: ${name}`);
  }
  for (const licence of Object.values(manifest.licenses)) {
    const actual = record(resolve(output, licence.file));
    if (actual.bytes !== licence.bytes || actual.sha256 !== licence.sha256) die(`licence drift: ${licence.file}`);
  }
  return { vertices, triangles: manifest.geometry.triangles, source_files: Object.keys(manifest.source_files).length, material_status: manifest.material.status };
}

const [mode, target, output] = process.argv.slice(2);
if (mode === '--export' && target && output && !process.argv[5]) console.log(JSON.stringify(await exportMesh(resolve(target), resolve(output))));
else if (mode === '--verify' && target && !output) console.log(JSON.stringify(verify(resolve(target))));
else die('usage: --export SOURCE_ROOT NEW_OUTPUT | --verify OUTPUT');
