// Development-only observation of source object identity, never a spatial join.
import assert from 'node:assert/strict';

export function inverseSourceFrame(group) {
  group.updateWorldMatrix(true, false);
  const matrix = group.matrixWorld.clone(), inverse = matrix.clone().invert();
  const m = matrix.elements, inv = inverse.elements;
  assert.equal(m.length, 16);
  assert.equal(inv.length, 16);
  assert(m.every(Number.isFinite) && inv.every(Number.isFinite));
  for (let col = 0; col < 4; col++) for (let row = 0; row < 4; row++) {
    let product = 0;
    for (let k = 0; k < 4; k++) product += m[k * 4 + row] * inv[col * 4 + k];
    assert(Math.abs(product - (col === row ? 1 : 0)) < 1e-9,
      'Source light parent frame is not invertible');
  }
  return { matrix, inverse };
}

export function observeVillageLightOwners(village) {
  const raw = village.getLightSources(), hung = village.pierInfo.hung;
  assert.equal(hung.length, 11, 'Pinned pier lantern census changed');
  assert.equal(village.lanterns.length, hung.length);
  const owners = new Map(), objects = new Set();
  for (const [ordinal, lantern] of village.lanterns.entries()) {
    const original = hung[ordinal];
    assert.equal(lantern.live, original.live, 'Lantern construction identity changed');
    assert.equal(lantern.rest, original.rest);
    assert.equal(lantern.pivot, original.pivot);
    assert.equal(lantern.obj.parent, village.group, 'Lantern is outside its source hierarchy');
    assert(!objects.has(lantern.obj) && !owners.has(lantern.live), 'Aliased lantern owner');
    assert.equal(raw.filter(light => light.position === lantern.live).length, 1,
      'Missing or ambiguous light position identity');
    objects.add(lantern.obj);
    owners.set(lantern.live, {
      id: `village-lantern-${ordinal}`, source_object: `Village.lanterns[${ordinal}].obj`,
      parent: 'world', ...inverseSourceFrame(lantern.obj),
      // Source construction ordinal; the catalogue supplies AssetId/placement later.
      binding: { producer_path: 'src/world/Village.js', producer_function: '_buildLanterns',
        collection: 'lanterns', ordinal, anchor: 'obj', evidence: 'live-vector-object-identity' },
    });
  }
  return owners;
}
