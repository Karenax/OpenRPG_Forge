// Capture actual source pendulum state and updates, without replacing randomness.
// Development acquisition only; the Forge runtime remains Rust/Bevy.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { resolve, relative, isAbsolute, join } from 'node:path';
import { pathToFileURL } from 'node:url';

const PIN = '1438b1abfcaee3267092b75573014f4d9b4a983c';
const [sourceArg, outputArg] = process.argv.slice(2);
assert(sourceArg && outputArg, 'Usage: node export-pier-motion.mjs SOURCE NEW_OUTPUT');
const source = resolve(sourceArg), output = resolve(outputArg), rel = relative(source, output);
assert(!existsSync(output) && rel && (rel.startsWith('..') || isAbsolute(rel)), 'Fresh external output required');
const git = (...args) => execFileSync('git', ['-C', source, ...args], { encoding: 'utf8' }).trim();
assert.equal(git('rev-parse', 'HEAD'), PIN);
assert.equal(git('status', '--porcelain=v1', '--untracked-files=all'), '');
const sha = bytes => createHash('sha256').update(bytes).digest('hex');
const paths = git('ls-files', '-z').split('\0').filter(name =>
  name.startsWith('src/') || ['LICENSE', 'test/headless.mjs', 'package.json', 'package-lock.json'].includes(name));
const originals = new Map(paths.map(name => [name, readFileSync(join(source, name))]));
const inputs = paths.map(name => ({ path:name, bytes:originals.get(name).length, sha256:sha(originals.get(name)) }));
const at = name => import(pathToFileURL(join(source, name)).href);
await at('test/headless.mjs');
const { GPU } = await at('src/engine/gpu/GPU.js');
await GPU.init({ headless:true });
try {
  const [{ Village }, { TerrainData }, { Colliders }, { Scene }, { G }] = await Promise.all([
    at('src/world/Village.js'), at('src/world/TerrainData.js'), at('src/world/Colliders.js'),
    at('src/engine/index.js'), at('src/engine/render/Frame.js'),
  ]);
  const village = new Village({ scene:new Scene(), terrain:new TerrainData(), colliders:new Colliders() });
  assert.equal(village.lanterns.length, 11);
  assert(village.sign && village._swing, 'Source sign state absent');
  const finite = value => { assert(Number.isFinite(value)); return value; };
  const vector = value => value.toArray().map(finite);
  const state = () => ({
    lanterns:village.lanterns.map((light, ordinal) => ({
      ordinal, owner_frame:`village-lantern-${ordinal}`,
      t:finite(light.t), ph:finite(light.ph), x:finite(light.x), z:finite(light.z),
      vx:finite(light.vx), vz:finite(light.vz),
      pivot_world_m:vector(light.pivot), rest_world_m:vector(light.rest),
      live_world_m:vector(light.live), rotation_xyzw:vector(light.obj.quaternion),
    })),
    sign:{ ...Object.fromEntries(Object.entries(village._swing).map(([name, value]) => [name, finite(value)])),
      rotation_xyzw:vector(village.sign.quaternion) },
  });
  const initial = state();
  const trace = [];
  const winds = [
    { speed:7, direction:[0.35 / Math.hypot(0.35,0.94), 0.94 / Math.hypot(0.35,0.94)] },
    { speed:0, direction:[1,0] },
    { speed:14, direction:[-0.8,0.6] },
    { speed:3, direction:[0,-1] },
  ];
  const deltas = [0,1/60,1/120,0.1,0.25,1/30];
  // Exercise both original pendulum routines through Village.update. No
  // reimplementation provides the expected trace, and no phase is reseeded.
  for (let index = 0; index < 240; index++) {
    const wind = winds[Math.floor(index / 60)];
    G.windSpeed.value = wind.speed;
    G.windDir.value.set(...wind.direction);
    const dt = deltas[index % deltas.length];
    village.update(dt);
    trace.push({ index, dt, wind, ...state() });
  }
  for (const input of inputs) {
    const bytes = readFileSync(join(source,input.path));
    assert.equal(bytes.length,input.bytes);
    assert.equal(sha(bytes),input.sha256,`Source drift: ${input.path}`);
  }
  assert.equal(git('status', '--porcelain=v1', '--untracked-files=all'), '');
  mkdirSync(output);
  const files = {};
  const write = (name, bytes) => {
    mkdirSync(resolve(output,name,'..'),{recursive:true});
    writeFileSync(join(output,name),bytes,{flag:'wx'});
    const pin = {file:name,bytes:bytes.length,sha256:sha(bytes)};
    files[name] = {bytes:pin.bytes,sha256:pin.sha256};
    return pin;
  };
  const sources = Object.fromEntries(inputs.map(input => [input.path,write(`source/${input.path}`,originals.get(input.path))]));
  const descriptor = write('pier-motion.json',Buffer.from(JSON.stringify({
    schema:'forge.tidewater.pier-motion-acquisition',version:1,source_commit:PIN,
    source_object_id:`tidewater:${PIN}:pier:0`,
    capture_mode:'original-village-update-headless-gpu',
    random_motion_phases_reacquisition_reproducible:false,
    existing_light_package_modified:false,
    initial, trace, source_inputs:inputs,
  },null,2)+'\n'));
  const notice = sources.LICENSE;
  assert(notice,'Original license required');
  write('manifest.json',Buffer.from(JSON.stringify({
    schema:'forge.tidewater.pier-motion-package',version:1,source_commit:PIN,
    descriptor,license:notice,sources,files,
    extractor_sha256:sha(readFileSync(new URL(import.meta.url))),
    status:'source-motion-capture-only',runtime_integrated:false,visual_parity:false,
  },null,2)+'\n'));
  console.log(JSON.stringify({output,lanterns:initial.lanterns.length,trace_frames:trace.length,
    descriptor_sha256:descriptor.sha256,status:'source-motion-capture-only'}));
} finally {
  GPU.device.destroy();
}
