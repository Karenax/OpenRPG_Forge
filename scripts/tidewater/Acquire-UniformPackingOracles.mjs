// Original source CPU packing, without initializing or replacing GPU APIs.
// This is acquisition tooling; the Forge runtime never imports JavaScript.
import fs from 'node:fs/promises';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';

const [acquisition, output, source] = process.argv.slice(2).map(p => path.resolve(p));
if (!acquisition || !output || !source) throw Error('Usage: Acquire-UniformPackingOracles.mjs ACQUISITION OUTPUT SOURCE');
const sha = data => createHash('sha256').update(data).digest('hex');
const read = name => fs.readFile(path.join(acquisition, name));
const raw = await read('capture.json');
const capture = JSON.parse(raw);
const receipt = JSON.parse(await read('capture.json.receipt.json'));
const inputsRaw = await read('source-inputs.json');
const inputs = JSON.parse(inputsRaw);
if (sha(raw) !== receipt.sha256 || sha(inputsRaw) !== receipt.source_inputs_sha256
  || capture.status !== 'complete' || capture.gpu_apis_replaced || inputs.commit !== receipt.commit
  || receipt.original_source_modified || receipt.gpu_apis_replaced) throw Error('Capture provenance differs');
const git = args => execFileSync('git', ['-C', source, ...args], { encoding: 'utf8', windowsHide: true }).trim();
if (git(['rev-parse', 'HEAD']) !== receipt.commit || git(['status', '--porcelain'])) throw Error('Source commit/cleanliness differs');
for (const pin of inputs.files) {
  const bytes = await fs.readFile(path.join(source, pin.path));
  if (bytes.length !== pin.bytes || sha(bytes) !== pin.sha256) throw Error(`Source input differs: ${pin.path}`);
}
const modulePath = path.join(source, 'src/engine/gpu/Uniforms.js');
const { UniformBlock } = await import(pathToFileURL(modulePath).href);
const blocks = new Map();
function add(block, site) {
  const key = JSON.stringify(block);
  if (!blocks.has(key)) blocks.set(key, { block, sites: [] });
  blocks.get(key).sites.push(site);
}
add(capture.frame, 'frame');
for (const material of capture.materials) add(material.uniforms, `material/${material.name}`);
for (const shader of capture.shaders) {
  for (const [group, bindings] of [[0, shader.group0], [1, shader.group1]]) {
    for (const binding of bindings) if (binding.uniform) {
      add(binding.uniform, `${shader.material}/${shader.pass.kind}/group${group}/${binding.name}`);
    }
  }
}
// Fail on an existing output. Failed or earlier acquisitions remain untouched.
await fs.mkdir(output);
await fs.mkdir(path.join(output, 'packed'));
const oracles = [];
const written = new Set();
for (const { block, sites } of blocks.values()) {
  const original = new UniformBlock(block.struct, Object.fromEntries(block.fields.map(f => [f.name, [f.type, f.value]])));
  if (original.byteLength !== block.bytes || !block.fields.every(f => {
    const layout = original.layout[f.name];
    return layout.offset === f.offset && layout.size === f.size && layout.stride === f.stride;
  })) throw Error(`Original source ABI differs: ${block.struct}`);
  original._pack();
  const bytes = new Uint8Array(original.data);
  const digest = sha(bytes);
  if (!written.has(digest)) {
    await fs.writeFile(path.join(output, 'packed', digest + '.bin'), bytes, { flag: 'wx' });
    written.add(digest);
  }
  oracles.push({ block, packed_sha256: digest, packed_bytes: bytes.length, sites });
}
const result = { schema: 'forge.source-uniform-packing-oracles', version: 1,
  capture_sha256: receipt.sha256, source_packer_sha256: sha(await fs.readFile(modulePath)),
  gpu_executed: false, oracles };
const bytes = JSON.stringify(result, null, 2) + '\n';
await fs.writeFile(path.join(output, 'oracles.json'), bytes, { flag: 'wx' });
await fs.writeFile(path.join(output, 'receipt.json'), JSON.stringify({
  recorded_utc: new Date().toISOString(), commit: receipt.commit, oracle_sha256: sha(bytes),
  helper_sha256: sha(await fs.readFile(new URL(import.meta.url))),
  source_inputs_sha256: sha(inputsRaw), source_input_count: inputs.files.length,
  unique_blocks: oracles.length, unique_packed_payloads: written.size,
  sites: oracles.reduce((n, o) => n + o.sites.length, 0),
  original_source_modified: false, gpu_apis_replaced: false, gpu_executed: false,
}, null, 2) + '\n', { flag: 'wx' });
console.log(JSON.stringify({ status: 'acquired-source-CPU-only', unique_blocks: oracles.length,
  unique_packed_payloads: written.size, oracle_sha256: sha(bytes) }));
