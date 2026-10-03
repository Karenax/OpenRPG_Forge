// Independent transport check against Rust's report and the previous capture.
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { createHash } from 'node:crypto';

const [root, rustPath, baselinePath, output] = process.argv.slice(2);
assert(root && rustPath && baselinePath && output,
  'NEW_PACKAGE RUST_REPORT BASELINE_DESCRIPTOR NEW_REPORT required');
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const read = file => JSON.parse(readFileSync(file, 'utf8').replace(/^\uFEFF/, ''));
const manifestBytes = readFileSync(join(root, 'manifest.json'));
const manifest = JSON.parse(manifestBytes), rust = read(rustPath), baseline = read(baselinePath);
assert.equal(hash(manifestBytes), rust.manifest_sha256);
const descriptorBytes = readFileSync(join(root, manifest.descriptor.file));
assert.equal(hash(descriptorBytes), rust.descriptor_sha256);
const descriptor = JSON.parse(descriptorBytes);
const receipt = read(join(root, manifest.acquisition.file));
const buildings = receipt.building_owner_observation;
const sides = receipt.side_owner_observation;
const vendors = receipt.vendor_owner_observation;
const expectedResolved = vendors ? 60 : sides ? 58 : buildings ? 51 : 19;
const expectedFrames = vendors ? 39 : sides ? 37 : buildings ? 32 : 13;
if (vendors) {
  assert(sides);
  assert.equal(vendors.schema, 'forge.source-vendor-light-owner-observation');
  assert.equal(vendors.version, 1); assert.equal(vendors.blocks.length, 2);
  assert.equal(vendors.frames.length, 2);
  assert.equal(vendors.full_vendor_constructors_executed, false);
  assert.equal(vendors.asset_fetch_executed, false);
}
if (sides) {
  assert(buildings);
  assert.equal(sides.schema, 'forge.source-village-side-light-owner-observation');
  assert.equal(sides.version, 1);
  assert.equal(sides.emitted_lights, 7); assert.equal(sides.records.length, 7);
  assert.equal(sides.records.filter(row => row.collection === 'boardwalk').length, 3);
  assert.equal(sides.records.filter(row => row.collection === 'plaza-prop').length, 1);
  assert.equal(sides.records.filter(row => row.collection === 'village-path-light').length, 3);
}
if (buildings) {
  assert.equal(buildings.schema, 'forge.source-building-light-owner-observation');
  assert.equal(buildings.version, 1);
  assert.equal(buildings.generator_bodies_changed, false);
  assert.equal(buildings.canonical_checkout_modified, false);
  assert.equal(buildings.unchanged_geometry_terrain_lights_colliders, true);
  assert.equal(buildings.building_invocations.length, 23);
  assert.equal(buildings.building_invocations.reduce((sum, row) => sum + row.emitted_lights, 0), 32);
  if ('second_construction_refused_before_generator' in buildings) {
    assert.equal(buildings.second_construction_refused_before_generator, true);
  }
}
assert.equal(descriptor.lights.length, 60); assert.equal(descriptor.frames.length, expectedFrames);
assert.equal(rust.emitters, 60); assert.equal(rust.payload_files_verified, 214);
assert.deepEqual(descriptor.source_inputs, baseline.source_inputs);
assert.deepEqual(descriptor.sets, baseline.sets);
const bindings = receipt.editable_ownership;
assert.equal(bindings.schema, 'forge.source-local-light-owner-observation');
assert.equal(bindings.version, 1);
assert.equal(bindings.resolved.length, expectedResolved);
assert.equal(bindings.unresolved_light_ids.length, 60 - expectedResolved);
assert.equal(bindings.source_binding_complete, expectedResolved === 60);
assert.equal(bindings.catalogue_binding_executed, false);
assert.equal(bindings.studio_projection_executed, false);
const ids = new Set([...bindings.resolved.map(row => row.light_id), ...bindings.unresolved_light_ids]);
assert.equal(ids.size, descriptor.lights.length);
assert.deepEqual([...ids].sort(), descriptor.lights.map(light => light.id).sort());
const resolved = new Map(bindings.resolved.map(row => [row.light_id, row]));
let rebound = 0, lanternsRebound = 0, buildingsRebound = 0, sidesRebound = 0, vendorsRebound = 0, differentPhases = 0;
for (const [index, light] of descriptor.lights.entries()) {
  const original = baseline.lights[index];
  const sourceProperties = row => {
    const { phase, owner_frame, position_local_m, direction_local, ...retained } = row;
    return retained;
  };
  // Registration identity, acquired world pose, defaults and photometry survive.
  assert.deepEqual(sourceProperties(light), sourceProperties(original));
  if (light.owner_frame !== original.owner_frame) {
    assert.equal(original.owner_frame, 'world');
    assert.equal(resolved.get(light.id)?.owner_frame, light.owner_frame);
    const binding = resolved.get(light.id)?.source_binding;
    if (binding.evidence === 'live-vector-object-identity') lanternsRebound++;
    else if (binding.evidence === 'audited-source-root-declaration-and-app-loop-order') {
      assert(vendors);
      assert.equal(light.owner_frame, `vendor-${binding.ordinal}`);
      assert.equal(binding.producer_path, vendors.blocks[binding.ordinal].path);
      const frame = descriptor.frames.find(frame => frame.id === light.owner_frame);
      assert.deepEqual(frame.world_from_local, vendors.frames[binding.ordinal].world_from_local);
      const local = [[-0.9, 1.85, 0.1], [-0.75, 1.58, -1.45]][binding.ordinal];
      light.position_local_m.forEach((value, axis) => assert(Math.abs(value - local[axis]) < 1e-12));
      vendorsRebound++;
    } else if (binding.evidence === 'generator-invocation-and-source-vector-identity') {
      assert(sides);
      const row = sides.records.find(row => row.collection === binding.collection && row.ordinal === binding.ordinal);
      assert(row && row.emitted_lights > 0);
      assert.equal(binding.producer_function, row.producer);
      assert.equal(binding.source_object_id, `tidewater:${descriptor.source_commit}:${row.collection}:${row.ordinal}`);
      const frame = descriptor.frames.find(frame => frame.id === light.owner_frame);
      assert.deepEqual(frame.world_from_local, row.world_from_local);
      const expectedTranslation = row.collection === 'boardwalk'
        ? [row.source_arguments.points[0][0], 0, row.source_arguments.points[0][1]]
        : row.source_arguments.slice(0, 3);
      assert.deepEqual(row.world_from_local.slice(12, 15), expectedTranslation);
      if (row.collection === 'plaza-prop') assert.equal(row.ordinal, 2);
      sidesRebound++;
    } else {
      assert(buildings);
      assert.equal(binding.evidence, 'generator-invocation-light-identity-and-builder-root-frame');
      const invocation = buildings.building_invocations[binding.ordinal];
      assert(invocation && invocation.emitted_lights > 0);
      assert.equal(binding.producer_function, invocation.producer);
      assert.equal(binding.source_object_id, `tidewater:${descriptor.source_commit}:building:${binding.ordinal}`);
      const frame = descriptor.frames.find(row => row.id === light.owner_frame);
      assert.deepEqual(frame.world_from_local, invocation.world_from_local);
      buildingsRebound++;
    }
    rebound++;
  } else {
    assert.deepEqual(light.position_local_m, original.position_local_m);
    assert.deepEqual(light.direction_local, original.direction_local);
  }
  if (resolved.has(light.id)) assert.equal(resolved.get(light.id).owner_frame, light.owner_frame);
  else assert.equal(light.owner_frame, 'world');
  const bits = Buffer.alloc(8); bits.writeDoubleBE(light.phase);
  assert.equal(bits.toString('hex'), rust.phase_f64_bits_hex[index]);
  if (!Object.is(light.phase, original.phase)) differentPhases++;
}
assert.equal(rebound, expectedResolved - 8);
assert.equal(lanternsRebound, 11); assert.equal(buildingsRebound, buildings ? 32 : 0);
assert.equal(sidesRebound, sides ? 7 : 0);
assert.equal(vendorsRebound, vendors ? 2 : 0);
assert.equal(descriptor.phase_reacquisition_reproducible, false);
assert.equal(rust.runtime_import_executed, false);
assert.equal(rust.gpu_executed, false);
const report = { schema: 'forge.source-light-owner-package-test', version: 1,
  recorded_utc: new Date().toISOString(), manifest_sha256: rust.manifest_sha256,
  descriptor_sha256: rust.descriptor_sha256, baseline_descriptor_sha256: hash(readFileSync(baselinePath)),
  emitters: 60, parent_frames: expectedFrames, editable_source_owners_resolved: expectedResolved,
  editable_source_owners_unresolved: 60 - expectedResolved,
  lanterns_rebound_by_identity: lanternsRebound, building_lights_rebound_at_emission: buildingsRebound,
  side_lights_rebound_at_generation: sidesRebound,
  vendor_lights_rebound_from_audited_declarations: vendorsRebound,
  phases_exact_rust_js: 60, phases_changed_since_previous_acquisition: differentPhases,
  second_construction_refused_before_generator: buildings?.second_construction_refused_before_generator ?? null,
  reacquisition_reproducible: false, unchanged_world_poses_and_photometry: true,
  studio_projection_executed: false, draw_executed: false, appearance_qualified: false };
writeFileSync(output, JSON.stringify(report, null, 2) + '\n', { flag: 'wx' });
console.log(JSON.stringify(report));
