// Pipeline construction on WebGPU; camera/mesh imports are explicit probe stubs.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { pathToFileURL } from 'node:url';

const [sourceArg, programArg, outputArg, preparedArg] = process.argv.slice(2);
if (!outputArg) throw Error('Usage: SOURCE PROGRAM NEW_OUTPUT [PREPARED_PROGRAM]');
const source = path.resolve(sourceArg), root = path.resolve(programArg), output = path.resolve(outputArg);
const sha = raw => crypto.createHash('sha256').update(raw).digest('hex');
const git = (...args) => execFileSync('git', ['-c', `safe.directory=${source.replaceAll('\\', '/')}`,
  '-C', source, ...args], { encoding: 'utf8' }).trim();
if (git('rev-parse', 'HEAD') !== '1438b1abfcaee3267092b75573014f4d9b4a983c'
    || git('status', '--porcelain', '--untracked-files=all')) throw Error('Clean reference required');
if (fs.existsSync(output)) throw Error('Fresh output required');
for (const parent of [source, root, ...(preparedArg ? [path.resolve(preparedArg)] : [])]) {
  const relative = path.relative(parent, output);
  if (!relative || (!relative.startsWith('..') && !path.isAbsolute(relative))) throw Error('External output required');
}
const descriptor = JSON.parse(fs.readFileSync(path.join(root, 'program.json')));
if (!['forge.tidewater.debris-nature-program', 'forge.tidewater.debris-scanned-program'].includes(descriptor.schema)
    || !['nature.wgsl', 'scanned.wgsl'].includes(descriptor.shader.file)) throw Error('Unsupported Debris program');
const shaderBytes = fs.readFileSync(path.join(root, descriptor.shader.file));
if (sha(shaderBytes) !== descriptor.shader.sha256 || shaderBytes.length !== descriptor.shader.bytes)
  throw Error('Prepared shader descriptor differs');
const programs = [['acquired', shaderBytes]];
if (preparedArg) programs.push(['shared-receivers', fs.readFileSync(path.join(preparedArg, descriptor.shader.file))]);
const importLine = '#import bevy_pbr::{mesh_functions, mesh_view_bindings::view,\n'
  + '    view_transformations::position_world_to_clip, pbr_types,\n'
  + '    pbr_functions::{apply_pbr_lighting,main_pass_post_lighting_processing}}';
const stub = `
struct ProbeView {world_position:vec3f};
const view=ProbeView(vec3f(0,0,2));
const identity=mat4x4f(vec4f(1,0,0,0),vec4f(0,1,0,0),vec4f(0,0,1,0),vec4f(0,0,0,1));
fn probeWorld(instance:u32)->mat4x4f{return identity;}
fn probeNormal(normal:vec3f,instance:u32)->vec3f{return normal;}
fn probeTag(instance:u32)->u32{return 0u;}
fn position_world_to_clip(P:vec3f)->vec4f{return vec4f(P,1);}
`;
const preprocess = (source, shared) => {
  const active = [true], conditions = [], lines = [];
  for (const line of source.split('\n')) {
    const condition = line.match(/^#ifdef (\w+)\s*$/);
    if (condition) {
      if (!['VILLAGE_DOUBLE_SIDED', 'FORGE_SHARED_SCENE_LIGHTING'].includes(condition[1])) throw Error('Unknown define');
      const enabled = condition[1] === 'FORGE_SHARED_SCENE_LIGHTING' && shared;
      conditions.push(enabled); active.push(active.at(-1) && enabled);
    } else if (line.trim() === '#else') {
      if (!conditions.length) throw Error('Unmatched else');
      active[active.length - 1] = active.at(-2) && !conditions.at(-1);
    } else if (line.trim() === '#endif') {
      if (!conditions.length) throw Error('Unmatched endif');
      conditions.pop(); active.pop();
    } else if (active.at(-1)) lines.push(line);
  }
  if (conditions.length) throw Error('Unclosed conditional');
  return lines.join('\n');
};
await import(pathToFileURL(path.join(source, 'test/headless.mjs')).href);
const { GPU } = await import(pathToFileURL(path.join(source, 'src/engine/gpu/GPU.js')).href);
await GPU.init({ headless: true });
const device = GPU.device, variants = [], payloads = [];
try {
  for (const [name, bytes] of programs) for (const shared of name === 'acquired' ? [false] : [false, true]) {
    let code = bytes.toString('utf8').replaceAll('\r\n', '\n');
    if (code.split(importLine).length !== 2) throw Error('Bevy import seam differs');
    code = preprocess(code.replace(importLine, stub), shared)
      .replaceAll('mesh_functions::get_world_from_local', 'probeWorld')
      .replaceAll('mesh_functions::mesh_normal_local_to_world', 'probeNormal')
      .replaceAll('mesh_functions::get_tag', 'probeTag')
      .replaceAll('#{MATERIAL_BIND_GROUP}', '0');
    if (/^#/m.test(code)) throw Error('Unresolved preprocessor');
    device.pushErrorScope('validation');
    const module = device.createShaderModule({ code });
    const info = await module.getCompilationInfo();
    const messages = info.messages.map(m => ({ type: m.type, message: m.message, line: m.lineNum }));
    if (messages.some(m => m.type === 'error')) throw Error(JSON.stringify(messages));
    await device.createRenderPipelineAsync({ layout: 'auto', vertex: { module, entryPoint: 'vertex', buffers: [{
      arrayStride: 60, attributes: [
        { shaderLocation: 0, offset: 0, format: 'float32x3' },
        { shaderLocation: 1, offset: 12, format: 'float32x3' },
        { shaderLocation: 2, offset: 24, format: 'float32x2' },
        { shaderLocation: 3, offset: 32, format: 'float32x3' },
        { shaderLocation: 4, offset: 44, format: 'float32x4' },
      ],
    }] }, fragment: { module, entryPoint: 'fragment', targets: [{ format: 'rgba16float' }] },
    primitive: { topology: 'triangle-list', cullMode: 'back' } });
    const error = await device.popErrorScope(); if (error) throw Error(error.message);
    const file = `${name}-${shared ? 'storage' : 'uniform'}.wgsl`, raw = Buffer.from(code);
    payloads.push([file, raw]); variants.push({ name, shared_scene_lighting: shared, source_sha256: sha(bytes),
      pipeline_created: true, shader: { file, bytes: raw.length, sha256: sha(raw) }, messages });
  }
  if (git('status', '--porcelain', '--untracked-files=all')) throw Error('Source changed');
  fs.mkdirSync(output); for (const [name, raw] of payloads) fs.writeFileSync(path.join(output, name), raw, { flag: 'wx' });
  const result = { status: 'passed', variants, script_sha256: sha(fs.readFileSync(import.meta.filename)),
    gpu_pipeline_created: true, bevy_imports_stubbed: true, scene_draw_executed: false,
    limits: ['Pipeline construction only; no real Bevy mesh/camera imports or resource bind groups',
      'No actual terrain pixel upload, scene draw, authoring gestures or visual parity qualification'] };
  fs.writeFileSync(path.join(output, 'result.json'), JSON.stringify(result, null, 2), { flag: 'wx' });
  console.log(JSON.stringify({ output, variants: variants.length, status: result.status }));
} finally { device.destroy(); }
