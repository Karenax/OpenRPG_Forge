import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { resolve, join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { buildingLightOwnerMap, prepareBuildingOwnerObservation } from '../source-building-light-owners.mjs';

const source = resolve(process.argv[2]);
assert.equal(execFileSync('git', ['-C', source, 'rev-parse', 'HEAD'], { encoding: 'utf8' }).trim(),
  '1438b1abfcaee3267092b75573014f4d9b4a983c');
const { Matrix4, Vector3 } = await import(pathToFileURL(join(source, 'src/engine/index.js')).href);
const a = { position: new Vector3(3, 4, 5) }, b = { position: new Vector3(6, 7, 8) };
const coincident = { position: a.position.clone() };
const frame = new Matrix4().makeTranslation(11, 0, -17);
const observations = [{ producer: 'fixture-no-lights', frame, lights: [] },
  { producer: 'fixture-two-lights', frame, lights: [a, b] }];
const raw = [a, b, coincident], village = { getLightSources: () => raw };
const owners = buildingLightOwnerMap(observations, village);
assert.equal(owners.size, 2); assert(!owners.has(coincident));
assert.equal(owners.get(a), owners.get(b)); // one owner frame, several lamps
assert.equal(owners.get(a).binding.ordinal, 1); // light-free calls keep their ordinal
const owner = owners.get(a);
const local = a.position.clone().applyMatrix4(owner.inverse);
assert(local.clone().applyMatrix4(owner.matrix).distanceTo(a.position) < 1e-12);
frame.elements[12] = 19;
assert.equal(owner.matrix.elements[12], 11); // captured frame is immutable by copy
let refusals = 0;
function rejects(rows, sources) {
  assert.throws(() => buildingLightOwnerMap(rows, { getLightSources: () => sources })); refusals++;
}
rejects(observations, [a, a, b]);
rejects(observations, [a, { ...b }]);
rejects([...observations, observations[1]], raw);
rejects([{ ...observations[1], frame: new Matrix4().makeScale(0, 1, 1) }], raw);
const bad = frame.clone(); bad.elements[0] = NaN;
rejects([{ ...observations[1], frame: bad }], raw);
await assert.rejects(prepareBuildingOwnerObservation({ sourceRoot: source,
  sourceCommit: '1438b1abfcaee3267092b75573014f4d9b4a983c', villageBytes: Buffer.from('not source'),
  expectedVillageHash: '0'.repeat(64) })); refusals++;
const villageBytes = readFileSync(join(source, 'src/world/Village.js'));
const options = { sourceRoot: source, sourceCommit: '1438b1abfcaee3267092b75573014f4d9b4a983c',
  villageBytes, expectedVillageHash: createHash('sha256').update(villageBytes).digest('hex') };
const scopeA = await prepareBuildingOwnerObservation(options);
const scopeB = await prepareBuildingOwnerObservation(options);
assert.notEqual(scopeA.observations, scopeB.observations);
assert.notEqual(scopeA.Village, scopeB.Village);
assert.equal(scopeA.receipt.observed_module_sha256, scopeB.receipt.observed_module_sha256);
console.log(JSON.stringify({ schema: 'forge.source-building-light-owner-test', version: 1,
  negative_checks: refusals, shared_frame_verified: true, coincident_unowned_refused: true,
  light_free_invocation_ordinal_preserved: true, captured_frame_copy_verified: true,
  separate_acquisitions_have_independent_modules: true,
  gpu_executed: false, studio_projection_executed: false }));
