// SPDX-License-Identifier: MIT
// Executes only the audited solar helpers from the pinned Tidewater reference.
import assert from 'node:assert/strict';
import { readFile, writeFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const [rootArg, outputArg] = process.argv.slice(2);
assert(rootArg && outputArg, 'Expected pinned reference root and fresh output file');
const root = resolve(rootArg), output = resolve(outputArg);
const commit = '1438b1abfcaee3267092b75573014f4d9b4a983c';
assert.equal(execFileSync('git', ['-C', root, 'rev-parse', 'HEAD'], { encoding: 'utf8' }).trim(), commit);
const inputs = ['src/sky/Sky.js', 'src/App.js', 'src/engine/math/Vector3.js',
  'src/engine/math/Quaternion.js', 'src/engine/math/MathUtils.js'];
execFileSync('git', ['-C', root, 'diff', '--exit-code', 'HEAD', '--', ...inputs]);
const files = new Map();
for (const path of inputs) {
  const bytes = await readFile(resolve(root, path));
  assert(bytes.length < 256 * 1024, 'Solar source exceeds audit bound');
  files.set(path, bytes);
}
const sky = files.get('src/sky/Sky.js').toString('utf8').replace(/\r\n/g, '\n');
const app = files.get('src/App.js').toString('utf8').replace(/\r\n/g, '\n');
// Sky's solar helper is its last export. The App helper is bounded by the next
// method, with no constructor, renderer, networking or application startup.
const solarText = sky.match(/export function sunDirectionFromTime\([^]*?\n\}/g);
assert.equal(solarText?.length, 1);
const updateText = app.match(/\tupdateSun\(\) \{([^]*?)\n\t\}\n\n\tapplyAtmosphereReadback\(/);
assert(updateText, 'Audited updateSun boundary changed');
const { Vector3 } = await import(pathToFileURL(resolve(root, 'src/engine/math/Vector3.js')));
const { MathUtils } = await import(pathToFileURL(resolve(root, 'src/engine/math/MathUtils.js')));
const solar = new Function('Vector3', 'MathUtils', `${solarText[0].replace('export ', '')}; return sunDirectionFromTime;`)(Vector3, MathUtils);
const G = { night: { value: 0 }, sunDir: { value: new Vector3() } };
const update = new Function('sunDirectionFromTime', 'MathUtils', 'Vector3', 'G', '_up',
  `return function() {${updateText[1]}\n};`)(solar, MathUtils, Vector3, G, new Vector3(0, 1, 0));
const samples = [];
for (const hours of [0, 5, 5.5, 6, 6.25, 12, 16.2, 17.5, 18, 18.25, 19, 24]) {
  for (const azimuth of [-180, 0, 73, 180]) {
    const fixture = { settings: { timeOfDay: hours, sunAzimuth: azimuth },
      atmosphere: { sunDir: { value: new Vector3() } },
      sky: { starIntensity: { value: 0 }, moonDir: { value: new Vector3() } } };
    update.call(fixture);
    const xyz = v => [v.x, v.y, v.z];
    samples.push({ hours, azimuth, sun: xyz(fixture.atmosphere.sunDir.value),
      moon: xyz(fixture.sky.moonDir.value), key: xyz(G.sunDir.value), night: G.night.value });
  }
}
const receipt = { schema: 'forge.source-solar-oracle', version: 1, source_commit: commit,
  source_inputs: [...files].map(([path, bytes]) => ({ path, sha256: createHash('sha256').update(bytes).digest('hex') })),
  source_helpers_executed: ['Sky.sunDirectionFromTime', 'App.updateSun'],
  application_started: false, gpu_executed: false, samples };
await writeFile(output, JSON.stringify(receipt, null, 2) + '\n', { flag: 'wx' });
console.log(JSON.stringify({ samples: samples.length, application_started: false, gpu_executed: false }));
