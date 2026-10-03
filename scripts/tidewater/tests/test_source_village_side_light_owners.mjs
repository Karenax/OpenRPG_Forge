import assert from 'node:assert/strict';
import { resolve, join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { sideLightOwnerMap, sideOwnerOverlay } from '../source-village-side-light-owners.mjs';

const source = resolve(process.argv[2]);
const { Matrix4, Vector3 } = await import(pathToFileURL(join(source, 'src/engine/index.js')).href);
const a = { position: new Vector3(3, 4, 5) }, b = { position: new Vector3(6, 7, 8) };
const live = new Vector3(9, 10, 11), prop = { position: live }, coincident = { position: live.clone() };
const frame = new Matrix4().makeTranslation(2, 0, -3);
const rows = [{ producer: 'buildBoardwalk', collection: 'boardwalk', ordinal: 0, frame, lights: [] },
  { producer: 'buildBoardwalk', collection: 'boardwalk', ordinal: 1, frame, lights: [a, b] },
  { producer: 'lampPost', collection: 'plaza-prop', ordinal: 2, frame, live }];
const raw = [a, b, prop, coincident], village = { getLightSources: () => raw };
const owners = sideLightOwnerMap(rows, village);
assert.equal(owners.size, 3); assert(!owners.has(coincident));
assert.equal(owners.get(a), owners.get(b)); assert.equal(owners.get(a).binding.ordinal, 1);
assert.equal(owners.get(prop).binding.ordinal, 2);
const local = live.clone().applyMatrix4(owners.get(prop).inverse);
assert(local.clone().applyMatrix4(owners.get(prop).matrix).distanceTo(live) < 1e-12);
let refusals = 0;
function rejects(records, sources) {
  assert.throws(() => sideLightOwnerMap(records, { getLightSources: () => sources })); refusals++;
}
rejects(rows, [a, b, prop, prop]);
rejects(rows, [a, b, { position: live.clone() }]);
rejects([{ ...rows[1], lights: [{ ...a }] }], raw);
rejects([rows[1], rows[1]], raw);
rejects([{ ...rows[1], frame: new Matrix4().makeScale(0, 1, 1) }], raw);
const bad = frame.clone(); bad.elements[0] = NaN;
rejects([{ ...rows[1], frame: bad }], raw);
assert.throws(() => sideOwnerOverlay('not the pinned import shape', source)); refusals++;
console.log(JSON.stringify({ schema: 'forge.source-village-side-owner-test', version: 1,
  negative_checks: refusals, coincident_unowned_refused: true, shared_frame_verified: true,
  empty_generator_ordinal_preserved: true, captured_pose_roundtrip_verified: true,
  gpu_executed: false, studio_projection_executed: false }));
