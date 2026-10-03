// Derive explicit construction identities while retaining the original lamps,
// including every captured random phase. No source producer is executed here.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { existsSync, lstatSync, mkdirSync, readFileSync, realpathSync, writeFileSync } from 'node:fs';
import { dirname, isAbsolute, join, relative, resolve } from 'node:path';

const [baseArg, basePin, outputArg] = process.argv.slice(2);
assert(baseArg && /^[a-f0-9]{64}$/.test(basePin ?? '') && outputArg,
  'BASE_ROOT BASE_PIN FRESH_OUTPUT required');
const sha = bytes => createHash('sha256').update(bytes).digest('hex');
const json = bytes => JSON.parse(bytes.toString('utf8'));
function readPackage(arg, pin) {
  const root = realpathSync(arg), manifestBytes = readFileSync(join(root, 'manifest.json'));
  assert(manifestBytes.length <= 2 * 1024 * 1024 && sha(manifestBytes) === pin, 'Manifest pin differs');
  const manifest = json(manifestBytes);
  assert.equal(manifest.schema, 'forge.source-local-lights.package');
  assert.equal(manifest.version, 1);
  assert.equal(manifest.renderer_parity, false); assert.equal(manifest.studio_qualified, false);
  assert.equal(manifest.status, 'source-local-light-acquisition-not-runtime-import');
  assert(Object.keys(manifest.files).length > 0 && Object.keys(manifest.files).length <= 4096);
  const members = new Map(); let total = 0;
  for (const [name, entry] of Object.entries(manifest.files)) {
    assert(name && !/[\\:\0]/.test(name) && !isAbsolute(name)
      && name.split('/').every(part => part && part !== '.' && part !== '..'), 'Unsafe member path');
    assert(Number.isSafeInteger(entry.bytes) && entry.bytes > 0 && entry.bytes <= 32 * 1024 * 1024);
    total += entry.bytes; assert(total <= 256 * 1024 * 1024);
    const path = join(root, name), rel = relative(root, realpathSync(path));
    assert(rel && !rel.startsWith('..') && !isAbsolute(rel) && lstatSync(path).isFile(), 'Member leaves package or is not regular');
    assert.equal(lstatSync(path).size, entry.bytes);
    const bytes = readFileSync(path); assert.equal(sha(bytes), entry.sha256, `Member changed: ${name}`);
    members.set(name, bytes);
  }
  for (const reference of [manifest.descriptor, manifest.acquisition, manifest.license, ...Object.values(manifest.sources)]) {
    assert.deepEqual(manifest.files[reference.file], { bytes: reference.bytes, sha256: reference.sha256 });
  }
  assert(manifest.acquisition.bytes <= 2 * 1024 * 1024);
  return { root, manifest, members, receipt: json(members.get(manifest.acquisition.file)) };
}
const base = readPackage(baseArg, basePin);
const before = base.receipt.building_owner_observation;
assert(before && before.unchanged_geometry_terrain_lights_colliders === true
  && before.generator_bodies_changed === false && before.canonical_checkout_modified === false);
assert.equal(before.source_commit, base.manifest.source_commit);
assert.equal(base.manifest.sources[before.producer_path].sha256, before.source_sha256);
assert.equal(before.schema, 'forge.source-building-light-owner-observation');
assert.equal(before.version, 1); assert(before.second_construction_refused_before_generator);
assert(before.building_invocations.length > 0 && before.building_invocations.length <= 4096);
const owners = base.receipt.editable_ownership.resolved.filter(row => row.source_binding.collection === 'building-construction');
const ids = new Set();
for (const [ordinal, row] of before.building_invocations.entries()) {
  // This is the existing asset importer's identity contract, already present
  // on every nonempty construction owner in this same acquisition.
  const id = `tidewater:${base.manifest.source_commit}:building:${ordinal}`;
  assert.equal(row.ordinal, ordinal);
  assert(Number.isSafeInteger(row.root_scopes) && row.root_scopes >= 1 && row.root_scopes <= 4096);
  if (row.emitted_lights === 0) assert.equal(row.root_scopes, 1, 'Empty construction has ambiguous root scopes');
  assert.equal(row.world_from_local.length, 16); assert(row.world_from_local.every(Number.isFinite));
  assert(Number.isSafeInteger(row.emitted_lights) && row.emitted_lights >= 0);
  const matched = owners.filter(owner => owner.source_binding.ordinal === ordinal);
  assert.equal(matched.length, row.emitted_lights, 'Same-capture ownership count differs');
  for (const owner of matched) {
    assert.equal(owner.source_binding.source_object_id, id);
    assert.equal(owner.source_binding.anchor, 'construction-root');
    assert.equal(owner.source_binding.producer_function, row.producer);
    assert(base.manifest.sources[owner.source_binding.producer_path], 'Owner source is not closed');
  }
  assert(row.source_object_id === undefined && row.anchor === undefined, 'Base is already enriched');
  ids.add(id); row.source_object_id = id; row.anchor = 'construction-root';
}
assert.equal(owners.length, before.building_invocations.reduce((sum,row) => sum + row.emitted_lights, 0));
const toolPin = sha(readFileSync(new URL(import.meta.url)));
base.receipt.building_identity_enrichment = {
  schema: 'forge.source-building-identity-enrichment', version: 1,
  base_manifest_sha256: basePin, base_acquisition_sha256: base.manifest.acquisition.sha256,
  derivation: 'same-capture-construction-ordinal-and-existing-owner-identity-contract',
  tool_sha256: toolPin, descriptor_bytes_preserved: true, lamp_phases_preserved: true,
};
const receiptBytes = Buffer.from(JSON.stringify(base.receipt, null, 2) + '\n');
assert(receiptBytes.length <= 2 * 1024 * 1024);
base.members.set(base.manifest.acquisition.file, receiptBytes);
base.manifest.acquisition.bytes = receiptBytes.length;
base.manifest.acquisition.sha256 = sha(receiptBytes);
base.manifest.files[base.manifest.acquisition.file] = { bytes: receiptBytes.length, sha256: sha(receiptBytes) };
const output = resolve(outputArg);
assert(!existsSync(output), 'Output must be fresh');
for (const root of [base.root]) {
  const rel = relative(root, output); assert(rel && (rel.startsWith('..') || isAbsolute(rel)), 'Output must be outside source packages');
}
mkdirSync(output);
for (const [name, bytes] of base.members) {
  const path = join(output, name); mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, bytes, { flag: 'wx' });
}
const manifestBytes = Buffer.from(JSON.stringify(base.manifest, null, 2) + '\n');
writeFileSync(join(output, 'manifest.json'), manifestBytes, { flag: 'wx' });
console.log(JSON.stringify({ schema: 'forge.source-building-identity-enrichment-result', version: 1,
  manifest_sha256: sha(manifestBytes), descriptor_sha256: base.manifest.descriptor.sha256,
  base_manifest_sha256: basePin,
  explicit_buildings: ids.size, explicit_empty_buildings: before.building_invocations.filter(row => row.emitted_lights === 0).length,
  lamp_descriptor_bytes_preserved: true, source_members_preserved: true, studio_executed: false, visual_parity: false }));
