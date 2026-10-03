// CPU-only acquisition of the unmodified, pinned MIT source boat.
// The GLB is an inspection/editing transport: procedural WGSL appearance and
// interactive controllers are retained separately, not claimed as GLB features.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import fs from 'node:fs/promises';
import { endianness } from 'node:os';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

const COMMIT = '1438b1abfcaee3267092b75573014f4d9b4a983c';
const POSITION_HASH = 'bd7c936ad56fe9b88cefa7c70124d5660651146e50f088e0ee660c080e7e2d9d';
const INDEX_HASH = '0cbb33e75432c18eb5a626bacedd2a975d07471be797f0c27af085cd54c07b56';
const GEOMETRY_HASH = '52b35be1ea38f49f9448ce8d8984d4d0bb3f3c3c506e73cbc1232a04d86ab92e';
const SUPPORT = ['src/App.js', 'src/player/Player.js', 'src/player/BoatController.js', 'src/player/BoatSpray.js', 'CREDITS.md', 'public/audio/CREDITS.md', 'public/audio/boat_rush.ogg', 'public/audio/boat_lap.ogg', 'public/audio/boat_engine.ogg', 'public/audio/hull_slap.ogg'];
const sha = bytes => createHash('sha256').update(bytes).digest('hex');
const json = value => Buffer.from(JSON.stringify(value, null, 2) + '\n');
const ascii = (a, b) => a < b ? -1 : a > b ? 1 : 0;

async function physicallyInside(upstream, relative) {
  const root = await fs.realpath(upstream), target = await fs.realpath(path.join(upstream, relative));
  const within = path.relative(root, target);
  assert(within && !within.startsWith('..' + path.sep) && !path.isAbsolute(within), 'Source resolves outside its physical checkout');
  assert(!(await fs.lstat(path.join(upstream, relative))).isSymbolicLink(), 'Linked source file is not audited');
}

function argumentsFor(argv) {
  const args = {};
  for (let i = 0; i < argv.length; i += 2) {
    const key = argv[i];
    assert(['--upstream', '--output'].includes(key) && argv[i + 1] && !args[key], 'Expected --upstream PATH --output NEW_PATH');
    args[key] = path.resolve(argv[i + 1]);
  }
  assert(args['--upstream'] && args['--output'], 'Both paths are required');
  const relative = path.relative(args['--upstream'], args['--output']);
  assert(relative && (relative.startsWith('..' + path.sep) || path.isAbsolute(relative)), 'Output must be outside the source checkout');
  return { upstream: args['--upstream'], output: args['--output'] };
}

function sourceGit(upstream) {
  const git = (...args) => execFileSync('git', ['-C', upstream, ...args], { encoding: 'utf8', windowsHide: true }).trim();
  assert.equal(git('rev-parse', 'HEAD'), COMMIT, 'Unexpected source revision');
  assert.equal(git('status', '--porcelain=v1', '--untracked-files=normal'), '', 'Source checkout must be clean');
}

// All imports/re-exports in the acquired graph must be static and local. This
// exporter calls no GPU initialization, network, upstream tools or lifecycle hooks.
async function sourceGraph(upstream) {
  const records = new Map();
  const visit = async relative => {
    relative = relative.replaceAll('\\', '/');
    if (records.has(relative)) return;
    assert(relative.startsWith('src/') && !relative.split('/').includes('..'), 'Module escapes the source graph');
    await physicallyInside(upstream, relative);
    const bytes = await fs.readFile(path.join(upstream, relative));
    const text = bytes.toString('utf8');
    records.set(relative, { path: relative, sha256: sha(bytes), bytes: bytes.length, source: bytes });
    assert(!/\bimport\s*\(/.test(text), `Dynamic import is not audited: ${relative}`);
    for (const match of text.matchAll(/(?:\b(?:import|export)\s+(?:[^;]*?\s+from\s*)?)["']([^"']+)["']/g)) {
      assert(match[1].startsWith('.'), `Nonlocal dependency is not audited: ${relative}: ${match[1]}`);
      await visit(path.posix.normalize(path.posix.join(path.posix.dirname(relative), match[1])));
    }
  };
  await visit('src/world/BoatModel.js');
  await visit('src/world/WorldLayout.js');
  return [...records.values()].sort((a, b) => ascii(a.path, b.path));
}

function proxyTopology(positions, indices) {
  const vertices = new Map(), welded = [], edges = new Map();
  for (let i = 0; i < positions.length; i += 3) {
    const key = Array.from(positions.subarray(i, i + 3), n => n === 0 ? '0' : String(n)).join(',');
    if (!vertices.has(key)) vertices.set(key, vertices.size);
    welded.push(vertices.get(key));
  }
  for (let i = 0; i < indices.length; i += 3) {
    const tri = Array.from(indices.subarray(i, i + 3));
    assert(tri.every(n => n < welded.length), 'Proxy index out of range');
    const p = tri.map(n => Array.from(positions.subarray(n * 3, n * 3 + 3)));
    const ab = p[1].map((n, j) => n - p[0][j]), ac = p[2].map((n, j) => n - p[0][j]);
    const cross = [ab[1] * ac[2] - ab[2] * ac[1], ab[2] * ac[0] - ab[0] * ac[2], ab[0] * ac[1] - ab[1] * ac[0]];
    assert(cross.some(n => n !== 0), 'Degenerate proxy triangle');
    for (const [start, end] of [[tri[0], tri[1]], [tri[1], tri[2]], [tri[2], tri[0]]]) {
      const a = welded[start], b = welded[end], key = `${Math.min(a, b)},${Math.max(a, b)}`;
      const edge = edges.get(key) || { count: 0, direction: 0 }; edge.count++; edge.direction += a < b ? 1 : -1; edges.set(key, edge);
    }
  }
  let boundary = 0;
  for (const edge of edges.values()) {
    if (edge.count === 1 && Math.abs(edge.direction) === 1) boundary++;
    else assert(edge.count === 2 && edge.direction === 0, 'Nonmanifold or inconsistently wound proxy');
  }
  assert.equal(boundary, 8, 'Pinned source boundary changed');
  return { kind: 'verified_source_proxy', boundary_edges: boundary, welded_vertices: vertices.size, edges: edges.size, nonmanifold_edges: 0, inconsistent_edges: 0, degenerate_triangles: 0, repairs_applied: false };
}

function scalarValue(value) {
  if (typeof value === 'number') { assert(Number.isFinite(value)); return value; }
  if (typeof value === 'boolean') return value;
  if (value?.isColor) return [value.r, value.g, value.b].map(scalarValue);
  if (value?.isVector2) return [value.x, value.y].map(scalarValue);
  if (value?.isVector3) return [value.x, value.y, value.z].map(scalarValue);
  if (value?.isVector4 || value?.isQuaternion) return [value.x, value.y, value.z, value.w].map(scalarValue);
  if (Array.isArray(value) || ArrayBuffer.isView(value)) return Array.from(value, scalarValue);
  if (value === null) return null;
  throw new Error('Unsupported source uniform or anchor value');
}

function transform(node) {
  return { translation_m: scalarValue(node.position), rotation_xyzw: scalarValue(node.quaternion), scale: scalarValue(node.scale) };
}

function bounds(array, width) {
  const min = Array(width).fill(Infinity), max = Array(width).fill(-Infinity);
  for (let i = 0; i < array.length; i++) {
    assert(Number.isFinite(array[i]), 'Nonfinite geometry');
    min[i % width] = Math.min(min[i % width], array[i]);
    max[i % width] = Math.max(max[i % width], array[i]);
  }
  return { min, max };
}

async function acquire(upstream, output) {
  assert.equal(endianness(), 'LE', 'Source typed-array transport requires a little-endian host');
  try { await fs.lstat(output); throw new Error('Output already exists; preserve it and choose a new candidate path'); }
  catch (error) { if (error.code !== 'ENOENT') throw error; }
  const sourceRoot = await fs.realpath(upstream), destinationParent = await fs.realpath(path.dirname(output));
  const physicalRelative = path.relative(sourceRoot, destinationParent);
  assert(physicalRelative && (physicalRelative.startsWith('..' + path.sep) || path.isAbsolute(physicalRelative)), 'Output parent resolves inside the source checkout');
  sourceGit(upstream);
  const graph = await sourceGraph(upstream);
  const graphPins = graph.map(({ path, sha256, bytes }) => ({ path, sha256, bytes }));
  const graphHash = sha(json(graphPins));
  await physicallyInside(upstream, 'LICENSE');
  const license = await fs.readFile(path.join(upstream, 'LICENSE'));
  assert(license.toString('utf8').startsWith('MIT License') && license.includes('Permission is hereby granted'));
  const supportSources = [];
  for (const relative of SUPPORT) {
    await physicallyInside(upstream, relative);
    const bytes = await fs.readFile(path.join(upstream, relative));
    assert(bytes.length <= 4 * 1024 * 1024, 'Unexpectedly large support file');
    supportSources.push({ path: relative, sha256: sha(bytes), bytes: bytes.length, source: bytes });
  }
  // Evaluate captured bytes rather than the mutable shared checkout. The local
  // source graph has no package dependencies, tools or runtime lifecycle scripts.
  const snapshot = output + '.source-graph';
  await fs.mkdir(snapshot, { recursive: false });
  await fs.writeFile(path.join(snapshot, 'package.json'), json({ type: 'module' }), { flag: 'wx' });
  for (const row of graph) {
    await fs.mkdir(path.dirname(path.join(snapshot, row.path)), { recursive: true });
    await fs.writeFile(path.join(snapshot, row.path), row.source, { flag: 'wx' });
    assert.equal(sha(await fs.readFile(path.join(snapshot, row.path))), row.sha256);
  }
  const { BoatModel } = await import(pathToFileURL(path.join(snapshot, 'src/world/BoatModel.js')).href);
  const { WORLD } = await import(pathToFileURL(path.join(snapshot, 'src/world/WorldLayout.js')).href);
  const { GPU } = await import(pathToFileURL(path.join(snapshot, 'src/engine/gpu/GPU.js')).href);
  const boat = new BoatModel();
  boat.group.updateMatrixWorld(true);
  assert.equal(GPU.device, null, 'Acquisition unexpectedly initialized a GPU');
  const files = new Map();
  const addFile = (name, bytes) => {
    const previous = files.get(name);
    if (previous) assert(previous.equals(bytes), 'Filename collision');
    else files.set(name, bytes);
    return { file: name, sha256: sha(bytes), bytes: bytes.length };
  };
  const addBlob = (bytes, extension) => addFile(sha(bytes) + extension, bytes);
  const materialRows = [], materialIds = new Map();
  const shaderModules = new Map();
  const moduleRow = module => {
    if (shaderModules.has(module)) return shaderModules.get(module);
    assert.equal(Object.keys(module.bindings).length, 0, `Unexpected module resource: ${module.name}`);
    const row = { name: module.name, code: addBlob(Buffer.from(module.code), '.wgsl'), dependencies: module.deps.map(moduleRow) };
    shaderModules.set(module, row);
    return row;
  };
  for (const [bucket, material] of Object.entries(boat.materials)) {
    if (!material?.isMaterial) continue;
    assert.equal(Object.keys(material.bindings).length, 0, 'Boat texture/storage binding needs explicit acquisition');
    const id = `boat-material-${bucket}`;
    materialIds.set(material, id);
    const snippets = {};
    for (const key of ['vertex', 'surface', 'output', 'shadow']) {
      snippets[key] = material[key] ? addBlob(Buffer.from(material[key]), '.wgsl') : null;
    }
    materialRows.push({
      id, source_name: material.name, bucket, snippets,
      modules: material.modules.map(moduleRow), attributes: material.attributes, varyings: material.varyings,
      defines: material.defines, lit: material.lit, side: material.side, transparent: material.transparent,
      blending: material.blending, depth_write: material.depthWrite, depth_test: material.depthTest,
      velocity_weight: material.velocityWeight, vertex_colors: material.vertexColors,
      uniforms: material.uniformBlock.order.map(name => ({ name, type: material.uniformBlock.layout[name].typeStr, value: scalarValue(material.uniforms[name].value) })),
    });
  }
  assert.equal(materialRows.length, 7);
  const nodes = [], parts = [], names = new Set();
  const visit = (node, parent) => {
    assert(node.name && !names.has(node.name), 'Every source node must have a stable unique name');
    names.add(node.name);
    const id = node.name, row = { id, parent, transform: transform(node), matrix_local: Array.from(node.matrix.elements), matrix_world: Array.from(node.matrixWorld.elements), visible: node.visible };
    nodes.push(row);
    if (node.isMesh) {
      const geometry = node.geometry;
      assert.equal(Object.keys(geometry.morphAttributes).length, 0, 'Morph data requires a different transport');
      assert.equal(geometry.groups.length, 0, 'Grouped source geometry requires explicit primitive splitting');
      const attributes = {};
      assert.deepEqual(Object.keys(geometry.attributes).sort(), ['aux', 'color', 'normal', 'position', 'uv']);
      for (const [name, width] of Object.entries({ position: 3, normal: 3, uv: 2, color: 3, aux: 4 })) {
        const a = geometry.attributes[name];
        assert(a.array instanceof Float32Array && !a.isInterleavedBufferAttribute && !a.normalized && a.itemSize === width);
        assert(a.count === geometry.attributes.position.count && a.count > 0);
        bounds(a.array, width);
        const bytes = Buffer.from(a.array.buffer, a.array.byteOffset, a.array.byteLength);
        attributes[name] = { ...addBlob(bytes, '.bin'), component_type: 'f32', item_size: width, count: a.count };
      }
      const indices = geometry.index.array;
      assert(indices instanceof Uint16Array || indices instanceof Uint32Array);
      assert(indices.length > 0 && indices.length % 3 === 0);
      for (const index of indices) assert(index < geometry.attributes.position.count, 'Index out of range');
      const material = materialIds.get(node.material);
      assert(material, 'Unacquired material');
      parts.push({ id, node: id, material, attributes,
        indices: { ...addBlob(Buffer.from(indices.buffer, indices.byteOffset, indices.byteLength), '.bin'), component_type: indices instanceof Uint16Array ? 'u16' : 'u32', count: indices.length },
        triangles: indices.length / 3, bounds_local: bounds(geometry.attributes.position.array, 3),
        cast_shadow: node.castShadow, receive_shadow: node.receiveShadow, render_order: node.renderOrder,
      });
    }
    for (const child of node.children) visit(child, id);
  };
  visit(boat.group, null);
  assert.equal(parts.length, 12);
  assert.equal(parts.reduce((n, p) => n + p.triangles, 0), boat.triangleCount);
  const hull = boat.createHullVolumeGeometry();
  const hullPositions = hull.attributes.position.array, hullIndices = hull.index.array;
  assert(hullPositions instanceof Float32Array && hullIndices instanceof Uint16Array);
  const positionBytes = Buffer.from(hullPositions.buffer, hullPositions.byteOffset, hullPositions.byteLength);
  const indexBytes = Buffer.from(hullIndices.buffer, hullIndices.byteOffset, hullIndices.byteLength);
  assert.equal(sha(positionBytes), POSITION_HASH); assert.equal(sha(indexBytes), INDEX_HASH);
  const canonicalIndices = Buffer.alloc(hullIndices.length * 4);
  hullIndices.forEach((value, i) => canonicalIndices.writeUInt32LE(value, i * 4));
  const counts = Buffer.alloc(16); counts.writeBigUInt64LE(BigInt(hullPositions.length / 3), 0); counts.writeBigUInt64LE(BigInt(hullIndices.length), 8);
  assert.equal(sha(Buffer.concat([counts, positionBytes, canonicalIndices])), GEOMETRY_HASH);
  const waterExclusion = { id: 'boat-water-exclusion', role: 'water_exclusion', parent: boat.group.name, parent_reference_kind: 'descriptor_node_id',
    transform: { translation_m: [0, 0, 0], rotation_xyzw: [0, 0, 0, 1], scale: [1, 1, 1] },
    positions: { ...addBlob(positionBytes, '.bin'), component_type: 'f32', item_size: 3, count: hullPositions.length / 3 },
    indices: { ...addBlob(indexBytes, '.bin'), component_type: 'u16', count: hullIndices.length },
    geometry_fingerprint: GEOMETRY_HASH, topology: proxyTopology(hullPositions, hullIndices),
    bounds_local: bounds(hullPositions, 3), source_method: 'BoatModel.createHullVolumeGeometry',
  };
  const sourceFiles = graph.map(row => ({ ...graphPins.find(pin => pin.path === row.path), original: addBlob(row.source, '.mjs') }));
  const supportFiles = supportSources.map(({ path: sourcePath, source, sha256, bytes }) => ({ path: sourcePath, sha256, bytes,
    role: sourcePath.endsWith('.ogg') ? 'source_audio' : sourcePath.includes('CREDITS') ? 'source_credits' : 'integration_reference_only',
    original: addBlob(source, path.extname(sourcePath).toLowerCase() === '.js' ? '.mjs' : path.extname(sourcePath).toLowerCase()),
    executed: false,
  }));
  const materials = addFile('boat-materials.json', json({ schema: 'forge.tidewater.boat-materials', version: 1, source_commit: COMMIT, materials: materialRows, appearance_qualification: 'source snippets and uniforms retained; Rust material port pending' }));
  const controls = [];
  for (const value of [-1, 0, 1]) {
    const model = new BoatModel(); model.setSteering(value); model.setThrottle(value); model.group.updateMatrixWorld(true);
    controls.push({ input: value, nodes: ['wheel', 'rudder', 'throttle'].map(key => ({ id: model.meshes[key].name, transform: transform(model.meshes[key]) })) });
  }
  const running = new BoatModel(); running.setPropellerRPM(300); running.update(1 / 60); running.group.updateMatrixWorld(true);
  const state = { time_step_s: 1 / 60, propeller_rpm: 300, nodes: ['propeller', 'radar'].map(key => ({ id: running.meshes[key].name, transform: transform(running.meshes[key]) })), flag_direction: scalarValue(running.materials.flagDir.value), flag_wind: running.materials.flagWind.value };
  const navigationLights = [false, true].map(on => { const model = new BoatModel(); model.setNavLights(on); return { input: on, material: 'boat-material-glow', uniform: 'navOn', value: model.materials.navOn.value }; });
  const partControls = [
    { id: 'steering-wheel', node: 'boat-wheel', pivot_node: 'boat-wheel-pivot', axis_local: [0, 0, 1], source_method: 'BoatModel.setSteering' },
    { id: 'steering-rudder', node: 'boat-rudder', axis_local: [0, 1, 0], source_method: 'BoatModel.setSteering' },
    { id: 'engine-throttle', node: 'boat-throttle', axis_local: [1, 0, 0], source_method: 'BoatModel.setThrottle' },
    { id: 'engine-propeller', node: 'boat-propeller', axis_local: [0, 0, 1], source_method: 'BoatModel.setPropellerRPM + update' },
    { id: 'radar-sweep', node: 'boat-radar', axis_local: [0, 1, 0], source_method: 'BoatModel.update' },
  ].map(row => ({ ...row, node_frame: nodes.find(n => n.id === row.node).transform, orientation_evidence: 'original node transform and declared rotation axis; source_declared' }));
  const glb = addFile('lobster-boat.glb', inspectionGlb(nodes, parts, materialRows, files, graphHash));
  const descriptor = addFile('boat.json', json({ schema: 'forge.tidewater.boat-source', version: 1, source_commit: COMMIT,
    source_id: `tidewater:${COMMIT}:boat:0`, source_graph_sha256: graphHash, source_files: sourceFiles, source_support_files: supportFiles,
    source_frame: { units: 'metres', axes: { x: 'port', y: 'up', z: 'forward' }, source_to_asset: 'identity' },
    canonical_placement: { translation_m: scalarValue(WORLD.boatDock.position), heading_radians: WORLD.boatDock.heading },
    nodes, parts, triangles: boat.triangleCount, material_library: materials, inspection_glb: glb, water_exclusion: waterExclusion,
    anchors: Object.fromEntries(['helmEye', 'boardPoint', 'exitPoints', 'propeller', 'rudder', 'bowSprayPoints'].map(name => [name, Array.isArray(boat[name]) ? boat[name].map(scalarValue) : scalarValue(boat[name])])),
    dimensions: boat.dimensions, hydrostatics: Object.fromEntries(Object.entries(boat.hydro).map(([key, value]) => [key, typeof value === 'number' ? scalarValue(value) : scalarValue(value)])),
    colliders: boat.colliders.map((c, i) => ({ id: `boat-collider-${i}`, tag: c.tag, center_m: scalarValue(c.center), half_extents_m: scalarValue(c.half), walkable: c.walkable, solid: c.solid })),
    hull_samples: boat.hullSamples.map(s => ({ ...s, position: scalarValue(s.position) })),
    controller_observations: { controls, running: state, part_controls: partControls, navigation_lights: navigationLights,
      shader_drivers: { material: 'boat-material-fittings', flag_pivot_m: scalarValue(boat.materials.flagPivot.value), uniforms: ['flagPivot', 'flagDir', 'flagWind'], frame_inputs: ['time', 'night', 'windDir', 'windSpeed'] },
      curves_baked_into_glb: false, source_controller: 'src/world/BoatModel.js' },
    qualification: { source_payloads: 'acquired', glb: 'inspection only; procedural appearance and interactive animations pending', rust_import: 'pending', studio_placement: 'pending', visual_parity: 'pending' },
  }));
  const licenseRef = addFile('LICENSE-Tidewater.txt', license);
  const after = (await sourceGraph(upstream)).map(({ path, sha256, bytes }) => ({ path, sha256, bytes }));
  assert.deepEqual(after, graphPins, 'Source changed during acquisition'); sourceGit(upstream);
  for (const row of supportSources) assert.equal(sha(await fs.readFile(path.join(upstream, row.path))), row.sha256, 'Support source changed during acquisition');
  for (const row of graph) assert.equal(sha(await fs.readFile(path.join(snapshot, row.path))), row.sha256, 'Evaluated snapshot changed during acquisition');
  assert.equal(GPU.device, null);
  const manifest = { schema: 'forge.tidewater.boat-package', version: 1, source_commit: COMMIT,
    boat: descriptor, materials, inspection_glb: glb, license: licenseRef,
    files: Object.fromEntries([...files.entries()].sort(([a], [b]) => ascii(a, b)).map(([name, bytes]) => [name, { bytes: bytes.length, sha256: sha(bytes) }])),
    qualification: 'source acquisition; Rust import, procedural appearance, Studio placement and runtime pending' };
  const staging = output + '.partial'; await fs.mkdir(staging, { recursive: false });
  for (const [name, bytes] of files) {
    await fs.writeFile(path.join(staging, name), bytes, { flag: 'wx' });
    assert.equal(sha(await fs.readFile(path.join(staging, name))), sha(bytes));
  }
  const manifestBytes = json(manifest); await fs.writeFile(path.join(staging, 'manifest.json'), manifestBytes, { flag: 'wx' });
  // Destination is still absent; failures preserve their separate staging folder.
  try { await fs.lstat(output); throw new Error('Output appeared during acquisition'); }
  catch (error) { if (error.code !== 'ENOENT') throw error; }
  await fs.rename(staging, output);
  console.log(JSON.stringify({ status: 'source-boat-acquired', source_commit: COMMIT, source_graph_files: graph.length, source_graph_sha256: graphHash, mesh_parts: parts.length, nodes: nodes.length, triangles: boat.triangleCount, colliders: boat.colliders.length, files: files.size, manifest_sha256: sha(manifestBytes), glb_sha256: glb.sha256, gpu_initialized: false }));
}

function inspectionGlb(nodes, parts, materials, files, graphHash) {
  const chunks = [], bufferViews = [], accessors = []; let offset = 0;
  const push = (reference, type, target, range) => {
    const bytes = files.get(reference.file); assert(bytes && sha(bytes) === reference.sha256);
    const pad = (4 - offset % 4) % 4; if (pad) { chunks.push(Buffer.alloc(pad)); offset += pad; }
    const view = bufferViews.length; bufferViews.push({ buffer: 0, byteOffset: offset, byteLength: bytes.length, target });
    chunks.push(bytes); offset += bytes.length;
    const accessor = accessors.length;
    accessors.push({ bufferView: view, componentType: { f32: 5126, u16: 5123, u32: 5125 }[reference.component_type], count: reference.count, type, ...(range || {}) });
    return accessor;
  };
  const meshes = parts.map(part => {
    const attributes = {};
    for (const [source, semantic, type] of [['position', 'POSITION', 'VEC3'], ['normal', 'NORMAL', 'VEC3'], ['uv', 'TEXCOORD_0', 'VEC2'], ['color', 'COLOR_0', 'VEC3'], ['aux', '_BOAT_AUX', 'VEC4']]) {
      attributes[semantic] = push(part.attributes[source], type, 34962, source === 'position' ? part.bounds_local : null);
    }
    return { name: part.id, primitives: [{ attributes, indices: push(part.indices, 'SCALAR', 34963), material: materials.findIndex(m => m.id === part.material), mode: 4 }], extras: { source_part: part.id, cast_shadow: part.cast_shadow, receive_shadow: part.receive_shadow } };
  });
  const gltfNodes = nodes.map(node => {
    const children = nodes.map((candidate, i) => candidate.parent === node.id ? i : -1).filter(i => i >= 0);
    return { name: node.id, translation: node.transform.translation_m, rotation: node.transform.rotation_xyzw, scale: node.transform.scale,
      ...(children.length ? { children } : {}),
      ...(parts.some(p => p.node === node.id) ? { mesh: parts.findIndex(p => p.node === node.id) } : {}),
      extras: { source_node: node.id, visible: node.visible },
    };
  });
  const gltfMaterials = materials.map(material => {
    const value = name => material.uniforms.find(u => u.name === name)?.value;
    const color = value('color') || [1, 1, 1];
    return { name: material.id, pbrMetallicRoughness: { baseColorFactor: [...color, value('opacity') ?? 1], metallicFactor: value('metalness') ?? 0, roughnessFactor: value('roughness') ?? 1 },
      alphaMode: material.transparent ? 'BLEND' : 'OPAQUE', doubleSided: material.side === 'double',
      extras: { source_name: material.source_name, appearance: 'inspection parameters only; original procedural WGSL is in boat-materials.json' },
    };
  });
  const doc = { asset: { version: '2.0', generator: 'OpenRPG Forge pinned Tidewater CPU acquisition' }, scene: 0,
    scenes: [{ name: 'LobsterBoat', nodes: nodes.map((node, i) => node.parent === null ? i : -1).filter(i => i >= 0) }],
    nodes: gltfNodes, meshes, materials: gltfMaterials, bufferViews, accessors, buffers: [{ byteLength: offset }],
    extras: { source_commit: COMMIT, source_graph_sha256: graphHash, water_exclusion_in_visible_scene: false, animated_shader_and_controllers: 'external source records; not baked', units: 'metres', source_to_asset: 'identity' } };
  const unpaddedJson = Buffer.from(JSON.stringify(doc)); const jsonPad = (4 - unpaddedJson.length % 4) % 4;
  const jsonBytes = Buffer.concat([unpaddedJson, Buffer.alloc(jsonPad, 0x20)]);
  const binPad = (4 - offset % 4) % 4; const binBytes = Buffer.concat([...chunks, Buffer.alloc(binPad)]);
  const header = Buffer.alloc(12); header.writeUInt32LE(0x46546c67, 0); header.writeUInt32LE(2, 4); header.writeUInt32LE(28 + jsonBytes.length + binBytes.length, 8);
  const chunkHeader = (bytes, type) => { const h = Buffer.alloc(8); h.writeUInt32LE(bytes.length, 0); h.writeUInt32LE(type, 4); return h; };
  return Buffer.concat([header, chunkHeader(jsonBytes, 0x4e4f534a), jsonBytes, chunkHeader(binBytes, 0x004e4942), binBytes]);
}

const { upstream, output } = argumentsFor(process.argv.slice(2));
await acquire(upstream, output);
