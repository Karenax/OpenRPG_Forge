// Development-only capture from real, pinned upstream objects.
// No Forge application depends on Node or the upstream renderer.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { inverseSourceFrame, observeVillageLightOwners } from './source-village-light-owners.mjs';

const PIN = '1438b1abfcaee3267092b75573014f4d9b4a983c';
const hash = value => createHash('sha256').update(value).digest('hex');
const finite = value => {
  assert(typeof value === 'number' && Number.isFinite(value), 'Nonfinite source light parameter');
  return value;
};
const vector = value => value.toArray().map(finite);
const optional = value => value == null ? null : finite(value);

// Village construction itself needs the source GPU for fish-prop buffers.
// The caller owns that audited acquisition; this helper initializes no GPU,
// replaces no SDK method and does not reconstruct source lighting profiles.
export function captureSourceLocalLights({ sourceCommit, lighting, village, boat, terrain,
  appFactories, sourceInputs, gpu, captureMode, extraVillageOwners = new Map(), vendorOwners = [] }) {
  assert.equal(captureMode, 'source-export-headless-gpu');
  assert(gpu?.device && gpu?.queue, 'The real source GPU must be initialized for Village acquisition');
  assert.equal(sourceCommit, PIN, 'Unexpected source revision');
  const { LocalLights, addVillageLights, addBoatLights } = lighting;
  assert.equal(typeof LocalLights, 'function');
  assert.equal(typeof addVillageLights, 'function');
  assert.equal(typeof addBoatLights, 'function');
  assert.equal(typeof appFactories?.addFloodlights, 'function');
  assert.equal(typeof appFactories?.addVendorLights, 'function');
  assert(sourceInputs.some(row => row.path === 'src/materials/LocalLights.js'), 'Source lighting pin is missing');
  const runtime = new LocalLights(), registered = [];
  const villageOwners = observeVillageLightOwners(village);
  const rawVillageLights = village.getLightSources();
  const allVillageFrames = new Map(Array.from(villageOwners.values(), owner => [owner.id, owner]));
  for (const owner of extraVillageOwners.values()) {
    assert(!allVillageFrames.has(owner.id) || allVillageFrames.get(owner.id) === owner,
      'Conflicting source owner frames');
    allVillageFrames.set(owner.id, owner);
  }
  const sink = { add(source) {
    // Retain the phase assigned by the actual source function, without reseeding.
    const result = runtime.add(source);
    assert.equal(result, source);
    registered.push(result);
    return result;
  } };
  const villageCount = village.getLightSources().length;
  addVillageLights(sink, village);
  assert.equal(registered.length, villageCount, 'Village light census changed');
  addBoatLights(sink, boat);
  assert.equal(registered.length, villageCount + 6, 'Pinned boat light census changed');
  appFactories.addFloodlights(sink, boat);
  assert.equal(registered.length, villageCount + 8, 'Pinned floodlight census changed');
  appFactories.addVendorLights(sink, terrain);
  assert.equal(registered.length, villageCount + 10, 'Pinned vendor light census changed');
  assert.equal(runtime.sources.length, registered.length);
  const boatFrame = inverseSourceFrame(boat.group);
  assert(vendorOwners.length === 0 || vendorOwners.length === 2, 'Incomplete vendor parent declarations');
  const boundVillageOwners = new Set();
  const groups = [
    { name: 'village', start: 0, count: villageCount, frame: 'world', factory: 'addVillageLights' },
    { name: 'boat', start: villageCount, count: 6, frame: 'boat-root', factory: 'addBoatLights' },
    { name: 'floodlights', start: villageCount + 6, count: 2, frame: 'boat-root', factory: 'Game.addFloodlights' },
    { name: 'vendors', start: villageCount + 8, count: 2, frame: 'world', factory: 'App.vendor-light-loop' },
  ];
  const records = registered.map((source, order) => {
    const group = groups.find(row => order >= row.start && order < row.start + row.count);
    assert(group, 'Source light has no registration owner');
    // The pinned factory emits once per raw entry, in source order. Windows
    // clone/offset their position, so their observed raw object is the binding.
    const raw = group.name === 'village' ? rawVillageLights[order - group.start] : null;
    const owner = raw ? extraVillageOwners.get(raw) ?? villageOwners.get(raw.position)
      : group.name === 'vendors' ? vendorOwners[order - group.start] : null;
    if (owner && raw) boundVillageOwners.add(owner.id);
    const frame = owner?.id ?? group.frame;
    const inverse = owner?.inverse ?? (frame === 'boat-root' ? boatFrame.inverse : null);
    const setOrder = order - group.start;
    if (source.update) source.update();
    const worldPosition = source.position.clone();
    const localPosition = inverse ? worldPosition.clone().applyMatrix4(inverse) : worldPosition;
    const worldDirection = source.dir ? source.dir.clone() : null;
    const localDirection = worldDirection && inverse
      ? worldDirection.clone().transformDirection(inverse) : worldDirection;
    assert(source.enabled === undefined || typeof source.enabled === 'boolean');
    assert(typeof source.kind === 'string');
    const phase = finite(source.phase);
    return {
      // IDs use the pinned registration identity, not an unseeded random phase.
      id: hash(PIN + '/LocalLights/' + group.name + '/' + setOrder),
      registration_order: order,
      owner_frame: frame,
      source_kind: source.kind,
      source_function: group.factory,
      position_local_m: vector(localPosition),
      direction_local: localDirection ? vector(localDirection) : null,
      acquired_position_world_m: vector(worldPosition),
      acquired_direction_world: worldDirection ? vector(worldDirection) : null,
      color_linear_rgb: [source.color.r, source.color.g, source.color.b].map(finite),
      intensity_at_1m: finite(source.intensity),
      range_m: finite(source.range),
      cos_inner: optional(source.cosInner),
      cos_outer: optional(source.cosOuter),
      enabled: source.enabled ?? null,
      scale: optional(source.scale),
      flicker: optional(source.flicker),
      phase,
      phase_source: 'upstream_LocalLights.add',
      source_has_pose_update: typeof source.update === 'function',
    };
  });
  assert.equal(boundVillageOwners.size, allVillageFrames.size, 'Source registration lost observed ownership');
  return {
    schema: 'forge.source-local-lights',
    version: 1,
    source_commit: PIN,
    capture_mode: captureMode,
    gpu_initialized: true,
    browser_required: false,
    registration_scope: 'App.LocalLights.village-boat-floodlights-vendors-default-gear',
    // Only emitted after all four source producers ran on one collector above.
    full_application_inventory: true,
    missing_owners: [],
    phase_reacquisition_reproducible: false,
    source_defaults: { enabled: true, scale: 1, flicker: 0 },
    coordinate_system: { units: 'metres', up: '+Y', handedness: 'right' },
    frames: [
      { id: 'world', source_object: 'source-world', parent: null },
      { id: 'boat-root', source_object: 'BoatModel.group', parent: 'world',
        world_from_local: Array.from(boatFrame.matrix.elements, finite) },
      ...Array.from(allVillageFrames.values(), owner => ({ id: owner.id,
        source_object: owner.source_object, parent: owner.parent,
        world_from_local: Array.from(owner.matrix.elements, finite) })),
      ...vendorOwners.map(owner => ({ id: owner.id, source_object: owner.source_object,
        parent: owner.parent, world_from_local: Array.from(owner.matrix.elements, finite) })),
    ],
    sets: Object.fromEntries(groups.map(row => [row.name, { start_order: row.start, count: row.count }])),
    source_inputs: sourceInputs,
    lights: records,
    runtime_selection_executed: false,
    renderer_parity: false,
    studio_qualified: false,
  };
}
