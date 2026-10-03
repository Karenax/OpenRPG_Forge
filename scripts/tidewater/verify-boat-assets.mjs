// Independent transport check: every GLB accessor must preserve the acquired
// source bytes and every source node must retain its local hierarchy/transform.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import fs from 'node:fs/promises';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

const args = {};
for (let i = 2; i < process.argv.length; i += 2) {
  const key = process.argv[i];
  assert(['--package', '--validator-module', '--report'].includes(key) && process.argv[i + 1] && !args[key]);
  args[key] = path.resolve(process.argv[i + 1]);
}
assert(args['--package'] && args['--validator-module'] && args['--report']);
const root = args['--package'];
const sha = bytes => createHash('sha256').update(bytes).digest('hex');
const manifestBytes = await fs.readFile(path.join(root, 'manifest.json'));
const manifest = JSON.parse(manifestBytes);
assert.equal(manifest.schema, 'forge.tidewater.boat-package');
assert.equal(manifest.version, 1);
assert.equal(manifest.source_commit, '1438b1abfcaee3267092b75573014f4d9b4a983c');
const files = new Map();
for (const [name, pin] of Object.entries(manifest.files)) {
  assert(/^[a-zA-Z0-9][a-zA-Z0-9._-]{0,127}$/.test(name) && name !== '.' && name !== '..');
  const bytes = await fs.readFile(path.join(root, name));
  assert.equal(bytes.length, pin.bytes); assert.equal(sha(bytes), pin.sha256);
  files.set(name, bytes);
}
const readRef = ref => {
  const bytes = files.get(ref.file); assert(bytes);
  assert.equal(bytes.length, ref.bytes); assert.equal(sha(bytes), ref.sha256);
  return bytes;
};
const boat = JSON.parse(readRef(manifest.boat));
const materials = JSON.parse(readRef(manifest.materials));
const glb = readRef(manifest.inspection_glb);
assert.equal(glb.readUInt32LE(0), 0x46546c67); assert.equal(glb.readUInt32LE(4), 2); assert.equal(glb.readUInt32LE(8), glb.length);
const jsonLength = glb.readUInt32LE(12); assert.equal(glb.readUInt32LE(16), 0x4e4f534a);
const gltf = JSON.parse(glb.subarray(20, 20 + jsonLength).toString('utf8'));
const binHeader = 20 + jsonLength;
assert.equal(glb.readUInt32LE(binHeader + 4), 0x004e4942);
const binary = glb.subarray(binHeader + 8);
assert.equal(binary.length, glb.readUInt32LE(binHeader));
assert.equal(gltf.buffers.length, 1); assert.equal(gltf.images?.length ?? 0, 0); assert.equal(gltf.animations?.length ?? 0, 0);
assert.equal(gltf.nodes.length, boat.nodes.length); assert.equal(gltf.meshes.length, boat.parts.length);
assert.equal(gltf.materials.length, materials.materials.length);
assert.equal(gltf.extras.water_exclusion_in_visible_scene, false);
const accessorBytes = (index, ref, type) => {
  const accessor = gltf.accessors[index], view = gltf.bufferViews[accessor.bufferView];
  assert.equal(accessor.count, ref.count); assert.equal(accessor.type, type);
  assert.equal(accessor.componentType, { f32: 5126, u16: 5123, u32: 5125 }[ref.component_type]);
  assert.equal(view.buffer, 0); assert(!view.byteStride && !accessor.sparse && !accessor.normalized);
  assert.equal(view.byteLength, ref.bytes);
  const start = (view.byteOffset || 0) + (accessor.byteOffset || 0);
  assert(binary.subarray(start, start + ref.bytes).equals(readRef(ref)), 'GLB changed source payload bytes');
};
let triangles = 0, accessorChecks = 0;
for (const [i, part] of boat.parts.entries()) {
  const mesh = gltf.meshes[i]; assert.equal(mesh.name, part.id); assert.equal(mesh.primitives.length, 1);
  const primitive = mesh.primitives[0]; assert.equal(primitive.mode, 4);
  assert.equal(gltf.materials[primitive.material].name, part.material);
  for (const [name, semantic, type] of [['position', 'POSITION', 'VEC3'], ['normal', 'NORMAL', 'VEC3'], ['uv', 'TEXCOORD_0', 'VEC2'], ['color', 'COLOR_0', 'VEC3'], ['aux', '_BOAT_AUX', 'VEC4']]) {
    accessorBytes(primitive.attributes[semantic], part.attributes[name], type); accessorChecks++;
  }
  accessorBytes(primitive.indices, part.indices, 'SCALAR'); accessorChecks++;
  triangles += part.indices.count / 3;
}
assert.equal(triangles, boat.triangles);
for (const [i, source] of boat.nodes.entries()) {
  const node = gltf.nodes[i]; assert.equal(node.name, source.id);
  assert.deepEqual(node.translation, source.transform.translation_m); assert.deepEqual(node.rotation, source.transform.rotation_xyzw); assert.deepEqual(node.scale, source.transform.scale);
  const children = boat.nodes.map((n, j) => n.parent === source.id ? j : -1).filter(j => j >= 0);
  assert.deepEqual(node.children || [], children);
}
const hull = boat.water_exclusion;
const positions = readRef(hull.positions), indices = readRef(hull.indices);
assert.equal(hull.positions.count, 952); assert.equal(hull.indices.count, 5436); assert.equal(hull.topology.boundary_edges, 8);
assert.equal(hull.topology.repairs_applied, false);
const counts = Buffer.alloc(16); counts.writeBigUInt64LE(BigInt(hull.positions.count), 0); counts.writeBigUInt64LE(BigInt(hull.indices.count), 8);
const u32 = Buffer.alloc(hull.indices.count * 4);
for (let i = 0; i < hull.indices.count; i++) u32.writeUInt32LE(indices.readUInt16LE(i * 2), i * 4);
assert.equal(sha(Buffer.concat([counts, positions, u32])), hull.geometry_fingerprint);
assert.equal(hull.geometry_fingerprint, '52b35be1ea38f49f9448ce8d8984d4d0bb3f3c3c506e73cbc1232a04d86ab92e');
assert.deepEqual(boat.canonical_placement, { translation_m: [64.5, 0, 36.5], heading_radians: 0 });
assert.equal(boat.colliders.length, 23); assert.equal(boat.nodes.length, 14); assert.equal(boat.parts.length, 12);
const validator = await import(pathToFileURL(path.join(args['--validator-module'], 'index.js')).href);
const validation = await validator.validateBytes(new Uint8Array(glb), { uri: 'lobster-boat.glb', format: 'glb', maxIssues: 0, writeTimestamp: false });
const result = { schema: 'forge.tidewater.boat-acquisition-verification', version: 1,
  status: validation.issues.numErrors === 0 ? 'transport-pass' : 'gltf-fail', manifest_sha256: sha(manifestBytes), glb_sha256: sha(glb),
  package_files_verified: files.size, glb_accessors_byte_identical: accessorChecks, hierarchy_nodes_verified: boat.nodes.length,
  visible_meshes: boat.parts.length, visible_triangles: triangles, source_colliders: boat.colliders.length,
  source_proxy_fingerprint: hull.geometry_fingerprint, source_proxy_boundary_edges: 8,
  khronos: validation, limits: { procedural_appearance: 'pending Rust port', interactive_animations: 'source observations only, not GLB curves', studio: 'not tested', runtime: 'not tested' } };
await fs.writeFile(args['--report'], JSON.stringify(result, null, 2) + '\n', { flag: 'wx' });
console.log(JSON.stringify({ status: result.status, files: files.size, accessors: accessorChecks, nodes: boat.nodes.length, triangles, khronos_errors: validation.issues.numErrors, khronos_warnings: validation.issues.numWarnings, khronos_infos: validation.issues.numInfos }));
assert.equal(validation.issues.numErrors, 0, 'Khronos glTF Validator rejected the inspection GLB');
