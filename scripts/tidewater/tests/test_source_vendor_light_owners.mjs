import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { resolve, join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { acquireVendorOwnerFrames } from '../source-vendor-light-owners.mjs';

const source = resolve(process.argv[2]), hash = bytes => createHash('sha256').update(bytes).digest('hex');
const { Group, Vector3 } = await import(pathToFileURL(join(source, 'src/engine/index.js')).href);
const { STAND } = await import(pathToFileURL(join(source, 'src/game/FishStand.js')).href);
const { CHANDLERY } = await import(pathToFileURL(join(source, 'src/game/Chandlery.js')).href);
const originals = new Map(['src/game/FishStand.js', 'src/game/Chandlery.js']
  .map(path => [path, readFileSync(join(source, path))]));
const sourceInputs = [...originals].map(([path, bytes]) => ({ path, bytes: bytes.length, sha256: hash(bytes) }));
const terrain = { heightAt: (x, z) => x * 0.01 + z * 0.02 };
const options = { sourceCommit: '1438b1abfcaee3267092b75573014f4d9b4a983c',
  sourceInputs, originals, Group, terrain, STAND, CHANDLERY };
const captured = acquireVendorOwnerFrames(options);
for (const [ordinal, spec] of [STAND, CHANDLERY].entries()) {
  const owner = captured.frames[ordinal];
  assert.deepEqual(owner.matrix.elements.slice(12, 15), [spec.x, terrain.heightAt(spec.x, spec.z), spec.z]);
  const local = new Vector3(-0.5, 1.3, 0.8);
  assert(local.clone().applyMatrix4(owner.matrix).applyMatrix4(owner.inverse).distanceTo(local) < 1e-12);
}
assert.equal(captured.receipt.full_vendor_constructors_executed, false);
assert.equal(captured.receipt.asset_fetch_executed, false);
let constructions = 0, refusals = 0;
class CountingGroup extends Group { constructor() { super(); constructions++; } }
function rejects(overrides) {
  assert.throws(() => acquireVendorOwnerFrames({ ...options, Group: CountingGroup, ...overrides }));
  assert.equal(constructions, 0); refusals++;
}
rejects({ sourceCommit: '0'.repeat(40) });
rejects({ sourceInputs: sourceInputs.slice(0, 1) }); // second pin fails before first block runs
const tampered = new Map(originals); tampered.set('src/game/Chandlery.js', Buffer.from('changed'));
rejects({ originals: tampered });
rejects({ sourceInputs: sourceInputs.map(row => ({ ...row, sha256: '0'.repeat(64) })) });
rejects({ sourceInputs: sourceInputs.map(row => ({ ...row, bytes: row.bytes + 1 })) });
console.log(JSON.stringify({ schema: 'forge.source-vendor-owner-test', version: 1,
  negative_checks: refusals, all_pins_checked_before_execution: true, pose_roundtrip_verified: true,
  full_vendor_constructors_executed: false, asset_fetch_executed: false, gpu_executed: false,
  studio_projection_executed: false }));
