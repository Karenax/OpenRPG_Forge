// Development acquisition; no dependency on this script in the shipped renderer.
// Original shader attribution and LICENSE are included in every acquired package.
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { resolve, join, relative, isAbsolute } from 'node:path';
import { pathToFileURL } from 'node:url';
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';

const [sourceArg, outputArg, ...extra] = process.argv.slice(2);
assert(sourceArg && outputArg && !extra.length, 'SOURCE NEW_OUTPUT_DIRECTORY required');
const source = resolve(sourceArg), output = resolve(outputArg);
const within = relative(source, output);
assert(within && (within.startsWith('..') || isAbsolute(within)), 'Output must be outside source');
assert(!existsSync(output), 'Output must be new');
const commit = '1438b1abfcaee3267092b75573014f4d9b4a983c';
const sha = bytes => createHash('sha256').update(bytes).digest('hex');
const git = (...args) => execFileSync('git', ['-C', source, ...args], { encoding: 'utf8' }).trim();
assert.equal(git('rev-parse', 'HEAD'), commit);
assert.equal(git('status', '--porcelain'), '');
const paths = git('ls-files', 'src', 'test/headless.mjs', 'LICENSE', 'package.json', 'package-lock.json').split('\n');
const inputs = paths.map(path => ({ path, sha256: sha(readFileSync(join(source, path))) }));
mkdirSync(output, { recursive: true });
const files = {};
const save = (name, bytes) => {
  writeFileSync(join(output, name), bytes, { flag: 'wx' });
  files[name] = sha(bytes);
  return name;
};
const moduleAt = path => import(pathToFileURL(join(source, path)).href);
await moduleAt('test/headless.mjs');
const { GPU } = await moduleAt('src/engine/gpu/GPU.js');
await GPU.init({ headless: true });
GPU.device.pushErrorScope('validation');
const { Atmosphere } = await moduleAt('src/sky/Atmosphere.js');
const { Sky } = await moduleAt('src/sky/Sky.js');
const { SkyProClouds } = await moduleAt('src/sky/SkyProClouds.js');
const { Environment } = await moduleAt('src/sky/Environment.js');
const { Scene } = await moduleAt('src/engine/index.js');
const fetchBefore = globalThis.fetch;
const assets = {};
globalThis.fetch = async url => {
  assert(['/clouds/baseShape64.bin', '/clouds/blueNoise.bin'].includes(url), `Unexpected fetch: ${url}`);
  const bytes = readFileSync(join(source, 'public', url.slice(1)));
  assets[url] = sha(bytes);
  return new Response(bytes);
};
try {
  const atmosphere = new Atmosphere(), sky = new Sky(atmosphere);
  const clouds = new SkyProClouds(null, atmosphere);
  await clouds.ready;
  sky.clouds = clouds;
  const environment = new Environment(null, new Scene(), sky);
  assert.equal(environment.size, 128);
  assert.equal(environment.steps.length, 15);
  const uniformIds = new Map(), resourceIds = new Map();
  const uniforms = [], resources = [];
  const deref = value => typeof value === 'function' ? value() : value;
  const resource = value => {
    const object = deref(value);
    if (typeof object === 'string') return { sampler: object };
    assert(object && typeof object === 'object', 'Missing binding resource');
    if (!resourceIds.has(object)) {
      const id = resources.length;
      resourceIds.set(object, id);
      const description = { id };
      for (const key of ['label', 'width', 'height', 'depth', 'format', 'dimension', 'mipLevelCount', 'sampleCount', 'count', 'type', 'byteLength']) {
        if (['string', 'number'].includes(typeof object[key])) description[key] = object[key];
      }
      resources.push(description);
    }
    return { resource: resourceIds.get(object) };
  };
  const bindingSet = set => set.names.map((name, binding) => {
    const spec = set.specs[name];
    // The original described.struct is a live UniformBlock, not serializable schema.
    // Its qualified byte layout is recorded once in uniforms below.
    const entry = { name, binding, layout: set.described[binding].layout, decl: set.described[binding].decl };
    if (spec.uniform) {
      const block = spec.uniform;
      if (!uniformIds.has(block)) {
        const id = uniforms.length;
        uniformIds.set(block, id);
        block._pack();
        const fields = Object.entries(block.layout).map(([name, value]) => ({
          name, offset: value.offset, size: value.size, stride: value.stride, type: value.typeStr,
        }));
        uniforms.push({ id, struct: block.structName, byte_length: block.byteLength, fields,
          initial_bytes: save(`uniform-${id}.bin`, Buffer.from(block.data)) });
      }
      entry.uniform = uniformIds.get(block);
    } else {
      const key = ['storageTexture', 'texture', 'storage', 'sampler', 'uniformBuffer'].find(key => spec[key] !== undefined);
      assert(key, `Unknown binding ${name}`);
      Object.assign(entry, resource(spec[key]));
      entry.kind = key;
      entry.live_provider = typeof spec[key] === 'function';
      for (const key of ['view', 'mip', 'offset', 'size', 'access', 'viewDimension']) if (spec[key] !== undefined) entry[key] = spec[key];
    }
    return entry;
  });
  const kernels = [];
  const acquire = (name, kernel, dispatch) => {
    assert(kernel && typeof kernel.source === 'string');
    assert(kernel.pipeline, `Pipeline unavailable: ${name}`);
    kernels.push({ name, label: kernel.label, entry_point: 'main', workgroup_size: kernel.workgroupSize,
      dispatch, shader: save(`${name}.wgsl`, kernel.source),
      groups: [bindingSet(kernel.group0), bindingSet(kernel.bindings)] });
  };
  for (let face = 0; face < 6; face++) acquire(`face-${face}`, environment.faceKernels[face], [16, 16, 1]);
  acquire('mips-a', environment._mips.kernelA, [4, 4, 6]);
  acquire('mips-b', environment._mips.kernelB, [1, 1, 6]);
  for (let target = 0; target < 2; target++) {
    for (let level = 0; level < 6; level++) {
      const size = Math.ceil((128 >> level) / 8);
      acquire(`filter-${target}-${level}`, environment.filterKernels[target][level], [size, size, 6]);
    }
    acquire(`sh-${target}`, environment.shKernels[target], [1, 1, 1]);
  }
  assert.equal(kernels.length, 22);
  for (let level = 0; level < 6; level++) assert.equal(environment.filterKernels[0][level].source, environment.filterKernels[1][level].source);
  assert.equal(environment.shKernels[0].source, environment.shKernels[1].source);
  await GPU.queue.onSubmittedWorkDone();
  const error = await GPU.device.popErrorScope();
  assert.equal(error, null, error?.message);
  for (const input of inputs) assert.equal(sha(readFileSync(join(source, input.path))), input.sha256);
  assert.equal(git('rev-parse', 'HEAD'), commit);
  assert.equal(git('status', '--porcelain'), '');
  save('LICENSE', readFileSync(join(source, 'LICENSE')));
  save('environment-programs.json', JSON.stringify({
    schema: 'forge.tidewater.environment-programs', version: 1, source_commit: commit,
    source_inputs: inputs, assets, kernels, uniforms, resources,
    renderer_started: false, environment_dispatched: false, source_pipeline_creation_validated: true,
    limitations: ['constructor_defaults_only', 'no_compute_output_readback', 'no_bevy_execution', 'no_visual_parity'],
    files,
  }, null, 2) + '\n');
  console.log(JSON.stringify({ output, programs: kernels.length, uniforms: uniforms.length, resources: resources.length, status: 'original-programs-acquired' }));
} finally {
  globalThis.fetch = fetchBefore;
  GPU.device.destroy();
}
process.exit(0);
