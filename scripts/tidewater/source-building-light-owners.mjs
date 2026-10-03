// Development-only import overlay. Original generators and their RNG calls run
// unchanged; no canonical checkout or source builder prototype is modified.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { sideOwnerOverlay } from './source-village-side-light-owners.mjs';

const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const PIN = '1438b1abfcaee3267092b75573014f4d9b4a983c';
let observationScope = 0;

export async function prepareBuildingOwnerObservation({ sourceRoot, sourceCommit, villageBytes,
  expectedVillageHash, includeSideOwners = false }) {
  assert.equal(sourceCommit, PIN);
  assert.equal(hash(villageBytes), expectedVillageHash, 'Village source pin differs');
  const importLine = "import { buildHouse, buildBoathouse, buildMarketStall, buildShed } from './village/Buildings.js';";
  const names = ['buildHouse', 'buildBoathouse', 'buildMarketStall', 'buildShed'];
  let code = villageBytes.toString('utf8');
  assert.equal(code.split(importLine).length, 2, 'Building imports changed');
  code = code.replace(importLine,
    `import { ${names.map(name => `${name} as original_${name}`).join(', ')} } from './village/Buildings.js';`);
  const side = includeSideOwners ? sideOwnerOverlay(code, sourceRoot) : null;
  if (side) code = side.code;
  code = code.replace(/from\s+(['"])(\.[^'"]+)\1/g,
    (_, q, path) => `from ${JSON.stringify(pathToFileURL(resolve(sourceRoot, 'src/world', path)).href)}`);
  const prelude = `
import assert from 'node:assert/strict';
export const observedBuildingOwners = [];
function observeBuildingOwner(producer, fn, ctx, spec) {
  const B = ctx.B, before = [...ctx.lights], depth = B.stack.length;
  const beforeFrame = [...B.frame.elements], saved = Object.getOwnPropertyDescriptor(B, 'push');
  const originalPush = B.push;
  const savedLightPush = Object.getOwnPropertyDescriptor(ctx.lights, 'push');
  const originalLightPush = ctx.lights.push, emitted = [];
  let frame = null, rootReference = null, roots = 0;
  Object.defineProperty(B, 'push', { configurable: true, writable: true, value: function(matrix) {
    const isRoot = this.stack.length === depth;
    const result = originalPush.call(this, matrix);
    if (isRoot) {
      roots++;
      if (!frame) { frame = this.frame.clone(); rootReference = this.frame; }
    }
    return result;
  }});
  Object.defineProperty(ctx.lights, 'push', { configurable: true, writable: true, value: function(...lights) {
    assert(frame && (B.frame === rootReference || B.stack.includes(rootReference)),
      'Building light was emitted outside its primary construction frame');
    emitted.push(...lights);
    return originalLightPush.apply(this, lights);
  }});
  let result;
  try { result = fn(ctx, spec); }
  finally {
    if (saved) Object.defineProperty(B, 'push', saved);
    else delete B.push;
    if (savedLightPush) Object.defineProperty(ctx.lights, 'push', savedLightPush);
    else delete ctx.lights.push;
  }
  assert(roots >= 1, 'Building has no construction frame');
  assert.equal(B.stack.length, depth, 'Building leaked its construction stack');
  assert.deepEqual(B.frame.elements, beforeFrame, 'Building leaked its construction pose');
  assert(ctx.lights.length >= before.length);
  assert(before.every((light, index) => ctx.lights[index] === light), 'Building changed prior light identities');
  const appended = ctx.lights.slice(before.length);
  assert.equal(appended.length, emitted.length, 'Building light append bypassed observation');
  assert(appended.every((light, index) => light === emitted[index]));
  observedBuildingOwners.push({ producer, frame, source_spec: JSON.parse(JSON.stringify(spec)),
    root_scopes: roots, lights: appended });
  return result;
}
${names.map(name => `const ${name} = (ctx, spec) => observeBuildingOwner('${name}', original_${name}, ctx, spec);`).join('\n')}
`;
  code = prelude + (side?.prelude ?? '') + code;
  // Give each acquisition its own module cache scope, without changing source
  // code bytes or emitting this temporary scope as a persistent source identity.
  const observed = await import('data:text/javascript;base64,' + Buffer.from(code).toString('base64')
    + `#forge-building-owner-observation-${++observationScope}`);
  if (side) {
    const plaza = observed.Village.prototype._plaza;
    observed.Village.prototype._plaza = function(...args) {
      observed.setOwnerPlazaActive(true);
      try { return plaza.apply(this, args); }
      finally { observed.setOwnerPlazaActive(false); }
    };
  }
  let constructed = false;
  class SingleCaptureVillage extends observed.Village {
    constructor(options) {
      assert(!constructed, 'A building owner observation permits only one Village construction');
      constructed = true;
      super(options);
    }
  }
  return { Village: SingleCaptureVillage, observations: observed.observedBuildingOwners,
    sideObservations: observed.observedSideOwners ?? [],
    receipt: { schema: 'forge.source-building-light-owner-observation', version: 1,
      source_commit: PIN, producer_path: 'src/world/Village.js', source_sha256: expectedVillageHash,
      observed_module_sha256: hash(code), generator_bodies_changed: false,
      canonical_checkout_modified: false } };
}

export function buildingLightOwnerMap(observations, village) {
  const raw = village.getLightSources(), owners = new Map();
  for (const [ordinal, observation] of observations.entries()) {
    const matrix = observation.frame.clone(), inverse = matrix.clone().invert();
    assert(matrix.elements.every(Number.isFinite) && Math.abs(matrix.determinant()) > 1e-12);
    const owner = { id: `building-${ordinal}`, parent: 'world', matrix, inverse,
      source_object: `Village.building-construction[${ordinal}]`,
      binding: { producer_path: 'src/world/village/Buildings.js',
        producer_function: observation.producer, collection: 'building-construction', ordinal,
        source_object_id: `tidewater:${PIN}:building:${ordinal}`, anchor: 'construction-root',
        evidence: 'generator-invocation-light-identity-and-builder-root-frame' } };
    for (const light of observation.lights) {
      assert.equal(raw.filter(source => source === light).length, 1,
        'Captured building light is missing or ambiguous in Village');
      assert(!owners.has(light), 'Building light has two owners');
      owners.set(light, owner);
    }
  }
  return owners;
}

// Boundaries include the full geometry and collision result, not just a count.
// Random animation phase is intentionally excluded; no draw runs here.
export function villageConstructionDigest({ terrain, colliders, village }) {
  village.group.updateWorldMatrix(true, true);
  const nodes = [];
  village.group.traverse(node => {
    if (!node.geometry) return;
    const geometry = node.geometry;
    const attributes = Object.fromEntries(Object.entries(geometry.attributes).map(([name, attr]) => {
      const array = attr.isInterleavedBufferAttribute ? attr.data.array : attr.array;
      return [name, { sha256: hash(Buffer.from(array.buffer, array.byteOffset, array.byteLength)),
        count: attr.count, item_size: attr.itemSize }];
    }));
    const indices = geometry.index?.array;
    nodes.push({ name: node.name, matrix: [...node.matrixWorld.elements], attributes,
      indices_sha256: indices ? hash(Buffer.from(indices.buffer, indices.byteOffset, indices.byteLength)) : null,
      draw_range: geometry.drawRange });
  });
  const maps = Object.fromEntries(['heights', 'rock', 'sand', 'path', 'gully', 'seagrass', 'rubble', 'scarp']
    .map(name => { const array = terrain[name];
      return [name, hash(Buffer.from(array.buffer, array.byteOffset, array.byteLength))]; }));
  const lights = village.getLightSources().map(light => ({ position: light.position.toArray(),
    color: light.color.toArray(), intensity: light.intensity, kind: light.kind }));
  return hash(JSON.stringify({ maps, nodes, lights, buildings: village.buildings,
    footprints: village.footprints, checks: village.foundationChecks,
    boxes: colliders.boxes.map(box => ({ center: box.center.toArray(), half: box.half.toArray(),
      yaw: box.rotY, walkable: box.walkable, solid: box.solid, tag: box.tag })),
    cylinders: colliders.cylinders }));
}
