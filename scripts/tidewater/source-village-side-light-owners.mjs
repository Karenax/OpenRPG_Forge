// Development-only observation; anchors match boardwalk/plaza conversion recipes.
import assert from 'node:assert/strict';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

export function sideOwnerOverlay(code, sourceRoot) {
  const boardwalkImport = "import { buildBoardwalk } from './village/Boardwalk.js';";
  const propsImport = 'bench, ropeCoil, pathLight, lampPost, bucket, wreck,';
  assert.equal(code.split(boardwalkImport).length, 2);
  assert.equal(code.split(propsImport).length, 2);
  code = code.replace(boardwalkImport,
    "import { buildBoardwalk as original_buildBoardwalk } from './village/Boardwalk.js';")
    .replace(propsImport,
      'bench as original_owner_bench, ropeCoil, pathLight as original_owner_pathLight, lampPost as original_owner_lampPost, bucket, wreck,');
  const builderUrl = pathToFileURL(resolve(sourceRoot, 'src/world/village/GeoBuilder.js')).href;
  const prelude = `
import { mat4 as owner_mat4 } from ${JSON.stringify(builderUrl)};
export const observedSideOwners = [];
let boardwalkOrdinal = 0, pathLightOrdinal = 0, plazaOrdinal = 0, plazaActive = false;
export const setOwnerPlazaActive = value => { plazaActive = value; };
const bench = (B, ...args) => {
  if (plazaActive) plazaOrdinal++;
  return original_owner_bench(B, ...args);
};
function observePropOwner(B, args, producer, collection, ordinal, original) {
  const before = [...B.frame.elements];
  const frame = B.frame.clone().multiply(owner_mat4(args[0], args[1], args[2]));
  const live = original(B, ...args);
  assert.deepEqual(B.frame.elements, before, 'Prop changed the caller construction frame');
  observedSideOwners.push({ producer, collection, ordinal, frame, live, source_arguments: args });
  return live;
}
const lampPost = (B, ...args) => {
  assert(plazaActive, 'Lamp post called outside observed plaza');
  return observePropOwner(B, args, 'lampPost', 'plaza-prop', plazaOrdinal++, original_owner_lampPost);
};
const pathLight = (B, ...args) => observePropOwner(B, args, 'pathLight',
  'village-path-light', pathLightOrdinal++, original_owner_pathLight);
const buildBoardwalk = (ctx, points, options) => {
  const before = [...ctx.lights], beforeFrame = [...ctx.B.frame.elements];
  const frame = ctx.B.frame.clone().multiply(owner_mat4(points[0][0], 0, points[0][1]));
  const ordinal = boardwalkOrdinal++;
  const result = original_buildBoardwalk(ctx, points, options);
  assert.deepEqual(ctx.B.frame.elements, beforeFrame, 'Boardwalk changed the caller frame');
  assert(before.every((light, index) => ctx.lights[index] === light));
  observedSideOwners.push({ producer: 'buildBoardwalk', collection: 'boardwalk', ordinal,
    frame, lights: ctx.lights.slice(before.length), source_arguments: { points, options } });
  return result;
};
`;
  return { code, prelude };
}

export function sideLightOwnerMap(observations, village) {
  const raw = village.getLightSources(), owners = new Map();
  for (const row of observations) {
    const matrix = row.frame.clone(), inverse = matrix.clone().invert();
    assert(matrix.elements.every(Number.isFinite) && Math.abs(matrix.determinant()) > 1e-12);
    const lights = row.lights ?? raw.filter(light => light.position === row.live);
    if (!row.lights) assert.equal(lights.length, 1, 'Prop has missing or ambiguous light vector');
    const owner = { id: `${row.collection}-${row.ordinal}`, parent: 'world', matrix, inverse,
      source_object: `Village.${row.collection}[${row.ordinal}]`, binding: {
        producer_path: row.producer === 'buildBoardwalk' ? 'src/world/village/Boardwalk.js' : 'src/world/Props.js',
        producer_function: row.producer, collection: row.collection, ordinal: row.ordinal,
        source_object_id: `tidewater:1438b1abfcaee3267092b75573014f4d9b4a983c:${row.collection}:${row.ordinal}`,
        anchor: 'conversion-translation-frame', evidence: 'generator-invocation-and-source-vector-identity' } };
    for (const light of lights) {
      assert.equal(raw.filter(candidate => candidate === light).length, 1);
      assert(!owners.has(light), 'Side light has two owners');
      owners.set(light, owner);
    }
  }
  return owners;
}
