// Evaluate the acquired fabric vertex program on real GPU data before scene staging.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { pathToFileURL } from 'node:url';

const [sourceArg, packageArg, outputArg] = process.argv.slice(2);
if (!outputArg) throw Error('Usage: SOURCE PACKAGE NEW_OUTPUT');
const source = path.resolve(sourceArg), root = path.resolve(packageArg), output = path.resolve(outputArg);
const sha = raw => crypto.createHash('sha256').update(raw).digest('hex');
const pin = '1438b1abfcaee3267092b75573014f4d9b4a983c';
const git = (...args) => execFileSync('git', ['-c', `safe.directory=${source.replaceAll('\\', '/')}`,
  '-C', source, ...args], { encoding: 'utf8' }).trim();
if (git('rev-parse', 'HEAD') !== pin || git('status', '--porcelain', '--untracked-files=all'))
  throw Error('Clean pinned source required');
if (fs.existsSync(output)) throw Error('Fresh output required');
for (const parent of [source, root]) {
  const relative = path.relative(parent, output);
  if (!relative || (!relative.startsWith('..') && !path.isAbsolute(relative))) throw Error('External output required');
}
const manifestBytes = fs.readFileSync(path.join(root, 'package.json'));
const manifest = JSON.parse(manifestBytes);
if (manifest.schema !== 'forge.tidewater.debris-package' || manifest.source_commit !== pin) throw Error('Package differs');
const read = reference => {
  const name = reference.file;
  if (name !== reference.sha256 + '.bin' || path.basename(name) !== name
      || manifest.files[name]?.bytes !== reference.bytes || manifest.files[name]?.sha256 !== reference.sha256)
    throw Error('Invalid reference');
  const bytes = fs.readFileSync(path.join(root, name));
  if (bytes.length !== reference.bytes || sha(bytes) !== reference.sha256) throw Error('Payload differs');
  return bytes;
};
const floats = reference => {
  const raw = read(reference);
  return Array.from({ length: raw.length / 4 }, (_, i) => raw.readFloatLE(i * 4));
};
const frames = JSON.parse(read(manifest.frames));
const contracts = JSON.parse(read(manifest.materials));
const fabric = contracts.source_contracts.fabric;
const vertexBytes = read(fabric.stages.vertex), vertex = vertexBytes.toString('utf8');
const rows = [], identities = [];
for (const object of frames.props) for (const part of object.parts) {
  if (part.material !== 'fabric') continue;
  const rest = floats(part.reference_position), local = floats(part.attributes.pos), data = floats(part.attributes.data);
  const count = rest.length / 3;
  if (local.length !== rest.length || data.length !== count * 4) throw Error('Attribute dimensions differ');
  identities.push({ source_id: object.source_id, vertices: count });
  for (let i = 0; i < count; i++) rows.push(...rest.slice(i * 3, i * 3 + 3), 0,
    ...local.slice(i * 3, i * 3 + 3), 0, ...object.source_frame.translation_m, 0,
    ...data.slice(i * 4, i * 4 + 4));
}
const count = rows.length / 16;
if (!count || !rows.every(Number.isFinite)) throw Error('No finite fabric vertices');
const weightedVertices = rows.filter((value, i) => i % 16 === 13 && value !== 0).length;
const flagVertices = rows.filter((value, i) => i % 16 === 14 && value >= 5000).length;
const animatedVertices = weightedVertices + flagVertices;
await import(pathToFileURL(path.join(source, 'test/headless.mjs')).href);
const { GPU } = await import(pathToFileURL(path.join(source, 'src/engine/gpu/GPU.js')).href);
await GPU.init({ headless: true });
const device = GPU.device;
try {
  const code = `
struct SourceFrame { time:f32,windDir:vec2f,windSpeed:f32,night:f32 };
var<private> frame:SourceFrame;
struct SourceVertex { position:vec3f,normal:vec3f,uv:vec2f,tint:vec3f,vdata:vec4f };
struct Output { vTint:vec3f,vData:vec4f };
struct Row { rest:vec4f,local:vec4f,anchor:vec4f,data:vec4f };
struct Result { original:vec4f,restored:vec4f,wrong:vec4f };
@group(0) @binding(0) var<storage,read> rows:array<Row>;
@group(0) @binding(1) var<storage,read_write> results:array<Result>;
@group(0) @binding(2) var<uniform> wind:vec4f;
fn sourceVertex(v:ptr<function,SourceVertex>,o:ptr<function,Output>) {
${vertex}
}
@compute @workgroup_size(64)
fn main(@builtin(global_invocation_id) id:vec3u) {
  if (id.x >= arrayLength(&rows)) { return; }
  frame.time=wind.w; frame.windDir=wind.xy; frame.windSpeed=wind.z;
  let row=rows[id.x]; var o:Output; var v:SourceVertex;
  v.vdata=row.data; v.position=row.rest.xyz;
  sourceVertex(&v,&o); let original=v.position;
  // Shader adapter: evaluate in source axes, then transfer displacement to local geometry.
  v.position=row.rest.xyz; sourceVertex(&v,&o);
  let restored=row.local.xyz+(v.position-row.rest.xyz)+row.anchor.xyz;
  v.position=row.local.xyz; sourceVertex(&v,&o);
  results[id.x]=Result(vec4f(original,1),vec4f(restored,1),vec4f(v.position+row.anchor.xyz,1));
}
`;
  device.pushErrorScope('validation');
  const module = device.createShaderModule({ code });
  const info = await module.getCompilationInfo();
  if (info.messages.some(m => m.type === 'error')) throw Error(JSON.stringify(info.messages));
  const pipeline = await device.createComputePipelineAsync({ layout: 'auto', compute: { module, entryPoint: 'main' } });
  const input = device.createBuffer({ size: rows.length * 4, usage: GPUBufferUsage.STORAGE | GPUBufferUsage.COPY_DST });
  device.queue.writeBuffer(input, 0, new Float32Array(rows));
  const result = device.createBuffer({ size: count * 48, usage: GPUBufferUsage.STORAGE | GPUBufferUsage.COPY_SRC });
  const mapped = device.createBuffer({ size: count * 48, usage: GPUBufferUsage.MAP_READ | GPUBufferUsage.COPY_DST });
  const uniform = device.createBuffer({ size: 16, usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST });
  const bindings = device.createBindGroup({ layout: pipeline.getBindGroupLayout(0), entries:
    [input, result, uniform].map((buffer, binding) => ({ binding, resource: { buffer } })) });
  const cases = [], retained = [];
  for (const direction of [[1, 0, 4], [0.6, 0.8, 12]]) for (const time of [0, 1, 4, 124]) {
    const wind = [...direction, time]; device.queue.writeBuffer(uniform, 0, new Float32Array(wind));
    const encoder = device.createCommandEncoder(), pass = encoder.beginComputePass();
    pass.setPipeline(pipeline); pass.setBindGroup(0, bindings); pass.dispatchWorkgroups(Math.ceil(count / 64)); pass.end();
    encoder.copyBufferToBuffer(result, 0, mapped, 0, count * 48); device.queue.submit([encoder.finish()]);
    await mapped.mapAsync(GPUMapMode.READ);
    const raw = Buffer.from(new Uint8Array(mapped.getMappedRange())); mapped.unmap();
    let maxCorrected = 0, maxWrong = 0, moved = 0;
    for (let i = 0; i < count; i++) {
      const point = offset => [0, 1, 2].map(axis => raw.readFloatLE(i * 48 + offset + axis * 4));
      const original = point(0), restored = point(16), wrong = point(32);
      if (![...original, ...restored, ...wrong].every(Number.isFinite)) throw Error('Non-finite GPU output');
      const distance = p => Math.hypot(...p.map((value, axis) => value - original[axis]));
      maxCorrected = Math.max(maxCorrected, distance(restored)); maxWrong = Math.max(maxWrong, distance(wrong));
      if (Math.hypot(...original.map((value, axis) => value - rows[i * 16 + axis])) > 0.0001) moved++;
    }
    if (maxCorrected > 0.0001 || (animatedVertices && !moved) || (!animatedVertices && moved))
      throw Error(JSON.stringify({ reason: 'Source phase reconstruction failed', wind, count, maxCorrected, maxWrong, moved }));
    const name = `wind-${cases.length}.f32.bin`; retained.push([name, raw]);
    cases.push({ wind, vertices: count, moved_vertices: moved, max_corrected_error_m: maxCorrected,
      max_local_phase_error_m: maxWrong, output: { file: name, bytes: raw.length, sha256: sha(raw) } });
  }
  if (animatedVertices && Math.max(...cases.map(c => c.max_local_phase_error_m)) < 0.005)
    throw Error('Negative control did not expose phase loss');
  const error = await device.popErrorScope(); if (error) throw Error(error.message);
  if (git('status', '--porcelain', '--untracked-files=all')) throw Error('Source changed');
  fs.mkdirSync(output);
  for (const [name, bytes] of [...retained, ['compute.wgsl', Buffer.from(code)]])
    fs.writeFileSync(path.join(output, name), bytes, { flag: 'wx' });
  const receipt = { status: 'passed', source_commit: pin, package_sha256: sha(manifestBytes),
    vertex_sha256: sha(vertexBytes), script_sha256: sha(fs.readFileSync(import.meta.filename)),
    compute_sha256: sha(Buffer.from(code)), identities, cases, weighted_vertices: weightedVertices,
    flag_vertices: flagVertices, phase_affects_acquired_layer: animatedVertices > 0, gpu_compute_executed: true,
    scene_draw_executed: false, limits: ['Fabric vertex displacement only; no scene material, shadow, or visual parity qualification',
      'Explicit probe winds and times; no claim about runtime clock synchronization'] };
  fs.writeFileSync(path.join(output, 'result.json'), JSON.stringify(receipt, null, 2), { flag: 'wx' });
  console.log(JSON.stringify({ output, vertices: count, cases: cases.length,
    max_corrected_error_m: Math.max(...cases.map(c => c.max_corrected_error_m)),
    max_local_phase_error_m: Math.max(...cases.map(c => c.max_local_phase_error_m)), status: receipt.status }));
} finally { device.destroy(); }
