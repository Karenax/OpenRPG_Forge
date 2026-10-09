// Source material contract acquisition only; no scene rendering or Rust qualification.
// Usage: SOURCE ATLAS_ROOT VERIFIED_ATLAS_RECEIPT NEW_OUTPUT
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {pathToFileURL} from 'node:url';
const [sourceArg,atlasArg,receiptArg,outputArg]=process.argv.slice(2);
if(!outputArg) throw Error('SOURCE ATLAS_ROOT RECEIPT NEW_OUTPUT required');
const source=path.resolve(sourceArg),atlas=path.resolve(atlasArg),output=path.resolve(outputArg);
const hash=raw=>crypto.createHash('sha256').update(raw).digest('hex');
const pin='1438b1abfcaee3267092b75573014f4d9b4a983c';
const git=(...args)=>execFileSync('git',['-c',`safe.directory=${source.replaceAll('\\','/')}`,'-C',source,...args],{encoding:'utf8'}).trim();
if(git('rev-parse','HEAD')!==pin||git('status','--porcelain','--untracked-files=all')||fs.existsSync(output)) throw Error('Clean pinned source and fresh output required');
for(const root of [source,atlas]){const rel=path.relative(root,output);if(!rel||(!rel.startsWith('..')&&!path.isAbsolute(rel))) throw Error('External output required');}
const raw=fs.readFileSync(path.join(atlas,'scanned-atlases.json')),index=JSON.parse(raw);
const receiptRaw=fs.readFileSync(receiptArg),receipt=JSON.parse(receiptRaw);
if(index.source_commit!==pin||receipt.status!=='PASS'||receipt.descriptor_sha256!==hash(raw)) throw Error('Verified atlas required');
for(const [name,sha] of Object.entries(index.source_inputs)) if(hash(fs.readFileSync(path.join(source,name)))!==sha) throw Error('Source contract differs');
const read=ref=>{if(path.basename(ref.file)!==ref.file) throw Error('Unsafe reference');const bytes=fs.readFileSync(path.join(atlas,ref.file));if(bytes.length!==ref.bytes||hash(bytes)!==ref.sha256) throw Error('Atlas payload differs');return bytes;};
const at=name=>import(pathToFileURL(path.join(source,name)).href);
await at('test/headless.mjs');
const {GPU}=await at('src/engine/gpu/GPU.js');
await GPU.init({headless:true});
try {
  const [{ScannedDebris},{Texture},{TerrainData},{TerrainGPU},{collectModules}]=await Promise.all([
    at('src/world/debris/ScannedDebris.js'),at('src/engine/gpu/Texture.js'),at('src/world/TerrainData.js'),at('src/world/TerrainGPU.js'),at('src/engine/gpu/Shader.js')]);
  // Wrappers carry real acquired mip-zero pixels; no material draw is submitted.
  const textures=Object.fromEntries(['albedo','normal','arm'].map(map=>{
    const ref=index.textures[map],bytes=read(ref);
    if(ref.width!==2048||ref.height!==2048||ref.mips!==12) throw Error('Atlas contract differs');
    return [map,new Texture({label:'debrisScan_'+map,width:ref.width,height:ref.height,format:ref.format,mips:true,data:new Uint8Array(bytes.subarray(0,2048*2048*4))})];
  }));
  const gpu=new TerrainGPU(new TerrainData(),null);
  const material=ScannedDebris.prototype._createMaterial.call({gpu,textures});
  const pending={},files={};
  const save=(name,value)=>{const bytes=Buffer.isBuffer(value)?value:Buffer.from(value);if(pending[name]&&!pending[name].equals(bytes)) throw Error('Payload collision');pending[name]=bytes;files[name]={bytes:bytes.length,sha256:hash(bytes)};return {file:name,...files[name]};};
  const uniform=block=>block?{struct:block.structName,bytes:block.byteLength,wgsl:block.wgsl,fields:Object.fromEntries(block.order.map(name=>[name,{type:block.layout[name].typeStr,offset:block.layout[name].offset,value:block.fields[name].value?.toArray?.()??block.fields[name].value}]))}:null;
  const bindings=values=>Object.fromEntries(Object.entries(values).map(([name,binding])=>{
    if(binding.uniform) return [name,{uniform:uniform(binding.uniform)}];
    if(binding.texture){const t=typeof binding.texture==='function'?binding.texture():binding.texture;return [name,{texture:{label:t.label,width:t.width,height:t.height,format:t.format,mips:t.mipLevelCount},sample_type:binding.sampleType??null}];}
    throw Error('Unsupported binding '+name);
  }));
  const contract={name:material.name,attributes:material.attributes,varyings:material.varyings,defines:material.defines,side:material.side,transparent:material.transparent,depth_write:material.depthWrite,alpha_test:material.alphaTest,
    uniforms:uniform(material.uniformBlock),bindings:bindings(material.bindings),
    stages:Object.fromEntries(['vertex','surface','shadow'].map(stage=>[stage,save(`scanned-${stage}.wgsl`,material[stage]??'')])),
    modules:collectModules(material.modules).map(m=>({name:m.name,dependencies:m.deps.map(d=>d.name),bindings:bindings(m.bindings),code:save(`module-${hash(Buffer.from(m.code))}.wgsl`,m.code)}))};
  for(const name of ['LICENSE','CREDITS.md']) save(name,read({file:name,...index.files[name]}));
  if(git('status','--porcelain','--untracked-files=all')) throw Error('Source changed');
  const descriptor={schema:'forge.tidewater.debris-scanned-material',version:1,source_commit:pin,source_inputs:index.source_inputs,atlas_descriptor_sha256:hash(raw),atlas_receipt_sha256:hash(receiptRaw),acquisition_sha256:hash(fs.readFileSync(import.meta.filename)),contract,files,
    limits:['Actual source _createMaterial factory only; no shader pipeline or draw qualification.','TerrainGPU initial sun and shore bindings are contract metadata, not acquired scene lighting.','Atlas mip chains are referenced, not regenerated; only mip-zero pixels supplied to texture wrappers.','Rust normal mapping, LOD shadow discard, common editing and visual parity remain open.']};
  fs.mkdirSync(output);for(const [name,bytes]of Object.entries(pending)) fs.writeFileSync(path.join(output,name),bytes,{flag:'wx'});
  const descriptorRaw=Buffer.from(JSON.stringify(descriptor,null,2));fs.writeFileSync(path.join(output,'scanned-material.json'),descriptorRaw,{flag:'wx'});
  console.log(JSON.stringify({status:'PASS',output,descriptor_sha256:hash(descriptorRaw),modules:contract.modules.length,files:Object.keys(files).length,rendered:false}));
}finally{GPU.device.destroy();}
