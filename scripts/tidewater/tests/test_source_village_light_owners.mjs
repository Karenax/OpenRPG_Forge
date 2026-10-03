// Uses the pinned engine's real scene graph/matrix math, without creating a GPU.
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { resolve, join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { observeVillageLightOwners } from '../source-village-light-owners.mjs';

const source = resolve(process.argv[2]);
const pin = execFileSync('git', ['-C', source, 'rev-parse', 'HEAD'], { encoding: 'utf8' }).trim();
assert.equal(pin, '1438b1abfcaee3267092b75573014f4d9b4a983c');
const { Group, Vector3 } = await import(pathToFileURL(join(source, 'src/engine/index.js')).href);
const group = new Group(), lights = [], hung = [], lanterns = [];
for (let ordinal = 0; ordinal < 11; ordinal++) {
  const obj = new Group(), pivot = new Vector3(ordinal, 2, -ordinal);
  obj.position.copy(pivot); group.add(obj);
  const rest = pivot.clone().add(new Vector3(0, -0.4, 0)), live = rest.clone();
  hung.push({ pivot, rest, live }); lanterns.push({ obj, pivot, rest, live });
  lights.push({ position: live });
}
// An unowned source at exactly the same coordinates must remain unbound.
const unowned = lights[0].position.clone(); lights.push({ position: unowned });
const village = { group, pierInfo: { hung }, lanterns, getLightSources: () => lights };
const first = observeVillageLightOwners(village);
assert.equal(first.size, 11); assert(!first.has(unowned));
for (const [position, owner] of first) {
  const local = position.clone().applyMatrix4(owner.inverse);
  assert(local.clone().applyMatrix4(owner.matrix).distanceTo(position) < 1e-12);
}
// Nonidentity ancestry and animated child pose: retain local position under
// the actual hierarchy, rather than applying the ancestor twice.
group.position.set(19, -3, 41); group.rotation.y = 0.7; group.scale.set(2, 1, 3);
lanterns[0].obj.rotation.z = 0.2;
group.updateWorldMatrix(true, true);
for (const [ordinal, lantern] of lanterns.entries()) {
  lantern.live.copy(new Vector3(0, -0.4, 0).applyMatrix4(lantern.obj.matrixWorld));
  assert.equal(lantern.live, hung[ordinal].live);
}
const moved = observeVillageLightOwners(village);
for (const [position, owner] of moved) {
  const local = position.clone().applyMatrix4(owner.inverse);
  assert(local.distanceTo(new Vector3(0, -0.4, 0)) < 1e-11);
  assert(local.clone().applyMatrix4(owner.matrix).distanceTo(position) < 1e-11);
}
let refusals = 0;
function rejects(mutate, restore) {
  mutate(); try { assert.throws(() => observeVillageLightOwners(village)); refusals++; }
  finally { restore(); }
  assert.equal(observeVillageLightOwners(village).size, 11);
}
rejects(() => lights.push(lights[0]), () => lights.pop());
const light = lights[0];
rejects(() => lights[0] = { position: light.position.clone() }, () => lights[0] = light);
const live = lanterns[0].live;
rejects(() => lanterns[0].live = live.clone(), () => lanterns[0].live = live);
rejects(() => group.remove(lanterns[0].obj), () => group.add(lanterns[0].obj));
rejects(() => group.scale.x = 0, () => group.scale.x = 2);
rejects(() => group.position.x = NaN, () => group.position.x = 19);
const last = hung[10];
rejects(() => hung.pop(), () => hung.push(last));
console.log(JSON.stringify({ schema: 'forge.source-light-owner-test', version: 1,
  source_commit: pin, observed_owners: 11, negative_checks: refusals,
  coincident_unowned_refused: true, moved_scaled_rotated_hierarchy_verified: true,
  gpu_executed: false, studio_projection_executed: false }));
