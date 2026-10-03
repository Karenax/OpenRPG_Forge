// SPDX-License-Identifier: MIT
// Source block: Tidewater App.js at 1438b1abfcaee3267092b75573014f4d9b4a983c.
// Copyright 2026 DRG Software Solutions LLC; see third_party/tidewater/LICENSE.
// Development-only acquisition adapter. Never edits the canonical checkout.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';

const PIN = '1438b1abfcaee3267092b75573014f4d9b4a983c';
const APP_SHA = '5954295de75325437ec5bc3e9379542c09b6a7f214a38b5a26e62f126bea2912';
const GAME_SHA = '23437c030e19c95787ca2ec414903fe971e25ff985cb5d34fa0de57d5fcfc339';
const VENDOR_LOOP_SHA = 'c85d39c0d6dda27e51538c8106e093c7191dbce7d950971da68784c067db32af';
const hash = bytes => createHash('sha256').update(bytes).digest('hex');

export function prepareSourceAppLightFactories({ sourceCommit, sourceInputs,
  appBytes, gameBytes, Game, Vector3, Color, STAND, CHANDLERY, defaultUpgrades, gearStats }) {
  assert.equal(sourceCommit, PIN);
  for (const [path, bytes, expected] of [
    ['src/App.js', appBytes, APP_SHA], ['src/game/Game.js', gameBytes, GAME_SHA],
  ]) {
    const pin = sourceInputs.find(row => row.path === path);
    assert(pin, 'Original factory source pin absent: ' + path);
    assert.equal(hash(bytes), expected, 'Original factory source changed: ' + path);
    assert.equal(pin.sha256, expected);
    assert.equal(pin.bytes, bytes.length);
  }
  assert.equal(typeof Game?.prototype?.addFloodlights, 'function');
  // The reviewed, hash-allowlisted loop is the only executed extracted code.
  // Lines 351..357 exclude App/Game constructors and all following callbacks.
  const vendorLoop = appBytes.toString('utf8').replaceAll('\r\n', '\n')
    .split('\n').slice(350, 357).join('\n') + '\n';
  assert.equal(hash(vendorLoop), VENDOR_LOOP_SHA, 'Audited App lamp block changed');
  const vendorFactory = new Function('Vector3', 'Color', 'STAND', 'CHANDLERY',
    '"use strict"; return function sourceVendorLights() {\n' + vendorLoop + '\n};')
    (Vector3, Color, STAND, CHANDLERY);
  const upgrades = defaultUpgrades(), stats = gearStats(upgrades);
  assert(Object.values(upgrades).every(level => level === 0));
  assert.equal(stats.deckLights, false, 'Capture requires explicit default gameplay gear');
  return {
    addFloodlights(lights, boat) {
      const owner = { floods: [] };
      Game.prototype.addFloodlights.call(owner, lights, boat);
      assert.equal(owner.floods.length, 2);
      // Game.applyGear retains this exact value for default upgrades.
      assert(owner.floods.every(light => light.scale === 0));
    },
    addVendorLights(lights, terrainData) {
      vendorFactory.call({ localLights: lights, terrainData });
    },
    receipt: {
      schema: 'forge.source-local-lights.factory-acquisition', version: 1,
      source_commit: PIN,
      floodlights: { source_path: 'src/game/Game.js', source_sha256: GAME_SHA,
        method: 'Game.addFloodlights', invocation: 'actual-prototype-method',
        method_text_sha256: hash(Game.prototype.addFloodlights.toString()) },
      vendor_lights: { source_path: 'src/App.js', source_sha256: APP_SHA,
        first_line: 351, last_line: 357, normalization: 'UTF-8/LF/trailing-newline',
        block_sha256: VENDOR_LOOP_SHA, invocation: 'audited-verbatim-block' },
      gameplay_state: { source: 'defaultUpgrades+gearStats', upgrades,
        deck_lights: stats.deckLights, saved_player_state_loaded: false },
      full_application_executed: false,
      renderer_parity: false, studio_qualified: false,
    },
  };
}
