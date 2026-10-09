// Exact ScannedDebris atlas assembly and source GPU mips, fed by acquired browser pixels.
// Usage: SOURCE RETAINED_SCANS BROWSER_PIXELS VERIFIED_PIXEL_RECEIPT NEW_OUTPUT
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { pathToFileURL } from 'node:url';
const [sourceArg, scansArg, pixelsArg, receiptArg, outputArg] = process.argv.slice(2);
if (!outputArg) throw Error('SOURCE SCANS PIXELS RECEIPT NEW_OUTPUT required');
const source = path.resolve(sourceArg), scans = path.resolve(scansArg), pixels = path.resolve(pixelsArg), output = path.resolve(outputArg);
const hash = raw => crypto.createHash('sha256').update(raw).digest('hex');
const pin = '1438b1abfcaee3267092b75573014f4d9b4a983c';
const git = (...args) => execFileSync('git', ['-c', `safe.directory=${source.replaceAll('\\','/')}`, '-C', source, ...args], {encoding:'utf8'}).trim();
if (git('rev-parse','HEAD') !== pin || git('status','--porcelain','--untracked-files=all') || fs.existsSync(output)) throw Error('Clean source/fresh output required');
for (const root of [source, scans, pixels]) { const rel = path.relative(root, output); if (!rel || (!rel.startsWith('..') && !path.isAbsolute(rel))) throw Error('External output required'); }
const retainedRaw = fs.readFileSync(path.join(scans,'scanned-source.json')), retained = JSON.parse(retainedRaw);
const pixelsRaw = fs.readFileSync(path.join(pixels,'browser-pixels.json')), decoded = JSON.parse(pixelsRaw);
const receiptRaw = fs.readFileSync(receiptArg), receipt = JSON.parse(receiptRaw);
const sourceInputs = Object.fromEntries(['src/world/debris/ScannedDebris.js','src/engine/gpu/Texture.js','src/engine/gpu/Mipmaps.js'].map(name => [name,hash(fs.readFileSync(path.join(source,name)))]));
if (retained.source_commit !== pin || decoded.source_commit !== pin || decoded.source_contract_sha256 !== sourceInputs['src/world/debris/ScannedDebris.js']
    || decoded.retained_descriptor_sha256 !== hash(retainedRaw) || receipt.status !== 'PASS' || receipt.descriptor_sha256 !== hash(pixelsRaw)) throw Error('Verified source pixel receipt differs');
const read = (root, ref) => {
  if (!ref || path.basename(ref.file) !== ref.file) throw Error('Unsafe input reference');
  const raw = fs.readFileSync(path.join(root,ref.file));
  if (raw.length !== ref.bytes || hash(raw) !== ref.sha256) throw Error('Input payload differs');
  return raw;
};
const at = name => import(pathToFileURL(path.join(source,name)).href);
await at('test/headless.mjs');
const {GPU} = await at('src/engine/gpu/GPU.js');
await GPU.init({headless:true});
GPU.device.pushErrorScope('validation');
try {
  const [{ScannedDebris,SCAN_ASSETS},{Texture},{TerrainData},{TerrainGPU},{Group}] = await Promise.all([
    at('src/world/debris/ScannedDebris.js'),at('src/engine/gpu/Texture.js'),at('src/world/TerrainData.js'),at('src/world/TerrainGPU.js'),at('src/engine/index.js')]);
  globalThis.__debrisFile = async url => {
    const name = url.slice(url.lastIndexOf('/')+1);
    const raw = read(scans, {file:name,...retained.files[name]});
    return raw.buffer.slice(raw.byteOffset,raw.byteOffset+raw.byteLength);
  };
  globalThis.__debrisImage = async url => {
    const name = url.slice(url.lastIndexOf('/')+1), ref = decoded.images[name];
    if (!ref || ref.width !== 1024 || ref.height !== 1024 || ref.row_order !== 'top-first') throw Error('Invalid acquired browser tile');
    const raw = read(pixels,ref);
    return {data:new Uint8Array(raw.buffer,raw.byteOffset,raw.byteLength),width:ref.width,height:ref.height};
  };
  const create = Texture.prototype._create;
  // Add COPY_SRC only; atlas copying and mip filtering remain the unchanged source methods.
  Texture.prototype._create = function(...args) {
    if (this.label.startsWith('debrisScan_') && !this.usageList.includes('copySrc')) this.usageList.push('copySrc');
    return create.apply(this,args);
  };
  let scanned;
  try {
    const terrain = new TerrainData(), gpu = new TerrainGPU(terrain,null);
    scanned = new ScannedDebris({gpu,instances:retained.instances.map(r=>({...r})),parent:new Group()});
    await scanned.promise;
    if (!scanned.ready || scanned.levels.length !== 3 || SCAN_ASSETS.join(',') !== retained.assets.map(a=>a.source_name).join(',')) throw Error('Source scanned loading failed');
    GPU.submit(); await GPU.queue.onSubmittedWorkDone();
  } finally { Texture.prototype._create = create; }
  const files = {}, pending = {};
  const save = (name,raw) => { pending[name]=raw; files[name]={bytes:raw.length,sha256:hash(raw)}; return {file:name,...files[name]}; };
  const textures = {};
  for (const map of ['albedo','normal','arm']) {
    const texture = scanned.textures[map];
    if (texture.width !== 2048 || texture.height !== 2048 || texture.mipLevelCount !== 12 || texture.format !== (map==='albedo'?'rgba8unorm-srgb':'rgba8unorm')) throw Error('Source atlas dimensions/format differ');
    const levels = [];
    for (let mip=0;mip<12;mip++) {
      const side=2048>>mip, row=side*4, padded=Math.ceil(row/256)*256;
      const buffer=GPU.device.createBuffer({size:padded*side,usage:GPUBufferUsage.COPY_DST|GPUBufferUsage.MAP_READ});
      const encoder=GPU.device.createCommandEncoder();
      encoder.copyTextureToBuffer({texture:texture.getGPU(),mipLevel:mip},{buffer,bytesPerRow:padded,rowsPerImage:side},{width:side,height:side,depthOrArrayLayers:1});
      GPU.queue.submit([encoder.finish()]); await buffer.mapAsync(GPUMapMode.READ);
      const mapped=new Uint8Array(buffer.getMappedRange()), raw=Buffer.alloc(row*side);
      for (let y=0;y<side;y++) raw.set(mapped.subarray(y*padded,y*padded+row),y*row);
      buffer.unmap(); buffer.destroy(); levels.push(raw);
    }
    textures[map]={...save(`scan-${map}.mips.rgba8.bin`,Buffer.concat(levels)),width:2048,height:2048,mips:12,format:texture.format,
      sampler:{address:'clamp-to-edge',mag:'linear',min:'linear',mipmap:'linear',anisotropy:8},level_sha256:levels.map(hash)};
  }
  for (const name of ['LICENSE','CREDITS.md']) save(name,read(scans,{file:name,...retained.files[name]}));
  const error=await GPU.device.popErrorScope(); if(error) throw Error(error.message);
  if (git('status','--porcelain','--untracked-files=all')) throw Error('Source changed');
  for(const [name,expected] of Object.entries(sourceInputs)) if(hash(fs.readFileSync(path.join(source,name)))!==expected) throw Error('Source input changed');
  const result={schema:'forge.tidewater.debris-scanned-atlases',version:1,source_commit:pin,source_inputs:sourceInputs,
    retained_descriptor_sha256:hash(retainedRaw),browser_pixels_sha256:hash(pixelsRaw),browser_receipt_sha256:hash(receiptRaw),
    acquisition_sha256:hash(fs.readFileSync(import.meta.filename)),textures,files,
    instrumentation:'Acquired browser pixels supplied through the documented __debrisImage hook; COPY_SRC added only to debrisScan textures. Source _load and generateMipmaps unchanged.',
    limits:['Source GPU atlas pixels/mips only; no Rust material draw, camera LOD/crossfade, Studio or complete visual parity.']};
  fs.mkdirSync(output); for(const [name,raw] of Object.entries(pending)) fs.writeFileSync(path.join(output,name),raw);
  fs.writeFileSync(path.join(output,'scanned-atlases.json'),JSON.stringify(result,null,2));
  console.log(JSON.stringify({status:'PASS',output,maps:3,mips:36,bytes:Object.values(files).reduce((s,r)=>s+r.bytes,0)}));
} finally { delete globalThis.__debrisFile; delete globalThis.__debrisImage; GPU.device.destroy(); }
