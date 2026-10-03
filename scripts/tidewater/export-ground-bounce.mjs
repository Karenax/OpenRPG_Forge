// Acquire the pinned source's actual sunlit-ground bounce map without editing it.
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const [sourceArg, environmentArg, outputArg] = process.argv.slice(2);
if (!sourceArg || !environmentArg || !outputArg || process.argv.length !== 5) {
  throw new Error('Usage: node export-ground-bounce.mjs SOURCE ENVIRONMENT_JSON NEW_DIRECTORY');
}
const source = resolve(sourceArg);
const output = resolve(outputArg);
const environmentBytes = readFileSync(resolve(environmentArg));
const environment = JSON.parse(environmentBytes);
const pin = '1438b1abfcaee3267092b75573014f4d9b4a983c';
const git = (...args) => execFileSync('git', ['-C', source, ...args], { encoding: 'utf8' }).trim();
const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
if (git('rev-parse', 'HEAD') !== pin || git('status', '--porcelain')) {
  throw new Error('Pinned clean Tidewater source required');
}
if (environment.source_commit !== pin || !Array.isArray(environment.sun_direction)
    || environment.sun_direction.length !== 3 || !environment.sun_direction.every(Number.isFinite)) {
  throw new Error('Pinned finite source environment required');
}
if (existsSync(output) || output.startsWith(source + '/') || output.startsWith(source + '\\')) {
  throw new Error('New output directory outside source required');
}

const moduleAt = path => import(pathToFileURL(join(source, path)).href);
await moduleAt('test/headless.mjs');
const { GPU } = await moduleAt('src/engine/gpu/GPU.js');
await GPU.init({ headless: true });
GPU.device.pushErrorScope('validation');
const { TerrainData } = await moduleAt('src/world/TerrainData.js');
const { TerrainGPU } = await moduleAt('src/world/TerrainGPU.js');
const { Village } = await moduleAt('src/world/Village.js');
const { Colliders } = await moduleAt('src/world/Colliders.js');
const { Scene } = await moduleAt('src/engine/index.js');
const { G } = await moduleAt('src/engine/render/Frame.js');
const { installGroundBounce } = await moduleAt('src/materials/GroundBounce.js');
const { Texture } = await moduleAt('src/engine/gpu/Texture.js');
const { readTexture } = await moduleAt('src/engine/gpu/Readback.js');

const terrain = new TerrainData();
new Village({ scene: new Scene(), terrain, colliders: new Colliders() });
const terrainGPU = new TerrainGPU(terrain);
G.sunDir.value.set(...environment.sun_direction);
const { map, raw } = installGroundBounce({ terrain: terrainGPU });
terrainGPU.updateSunShadow(null, true);
GPU.submit();
await GPU.queue.onSubmittedWorkDone();

// The source maps are sampled/storage textures, not COPY_SRC. Copy each with an
// identity compute pass into a readback-capable texture without changing it.
const shader = GPU.device.createShaderModule({ code: `
@group(0) @binding(0) var inputTex: texture_2d<f32>;
@group(0) @binding(1) var outputTex: texture_storage_2d<rgba16float, write>;
@compute @workgroup_size(8, 8)
fn main(@builtin(global_invocation_id) id: vec3u) {
  if (any(id.xy >= textureDimensions(outputTex))) { return; }
  textureStore(outputTex, vec2i(id.xy), textureLoad(inputTex, vec2i(id.xy), 0));
}` });
const pipeline = GPU.device.createComputePipeline({ layout: 'auto', compute: { module: shader, entryPoint: 'main' } });
const files = {};
mkdirSync(output, { recursive: true });
for (const [name, input] of [['ground-bounce-raw.rgba16.bin', raw], ['ground-bounce.rgba16.bin', map]]) {
  if (input.width !== 512 || input.height !== 512 || input.format !== 'rgba16float') {
    throw new Error('Unexpected source ground bounce texture');
  }
  const copied = new Texture({ width: 512, height: 512, format: 'rgba16float', usage: ['storage', 'copySrc'] });
  const group = GPU.device.createBindGroup({ layout: pipeline.getBindGroupLayout(0), entries: [
    { binding: 0, resource: input.getGPU().createView() },
    { binding: 1, resource: copied.getGPU().createView() },
  ] });
  const pass = GPU.getEncoder().beginComputePass();
  pass.setPipeline(pipeline);
  pass.setBindGroup(0, group);
  pass.dispatchWorkgroups(64, 64);
  pass.end();
  const bytes = Buffer.from((await readTexture(copied)).data);
  if (bytes.length !== 512 * 512 * 8) throw new Error('Incomplete source ground bounce readback');
  writeFileSync(join(output, name), bytes, { flag: 'wx' });
  files[name] = sha256(bytes);
  copied.destroy();
}
const error = await GPU.device.popErrorScope();
if (error) throw new Error(error.message);
if (git('status', '--porcelain')) throw new Error('Source modified during acquisition');
const metadata = {
  schema: 'forge.tidewater.ground-bounce-probe', version: 1, source_commit: pin,
  environment_sha256: sha256(environmentBytes), width: 512, height: 512,
  format: 'rgba16float', sun_direction: environment.sun_direction,
  sea_level: G.seaLevel.value, strength: 1, files,
  limitations: ['fixed_sun', 'no_dynamic_native_bake', 'cloud_shadow_applied_at_shading_not_in_map'],
};
writeFileSync(join(output, 'ground-bounce.json'), JSON.stringify(metadata, null, 2) + '\n', { flag: 'wx' });
console.log(JSON.stringify({ output, files, status: 'ground_bounce_acquired' }));
GPU.device.destroy();
process.exit(0);
