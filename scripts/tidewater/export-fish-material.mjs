// SPDX-License-Identifier: MIT
// Acquire generated source material contracts without modifying the pinned reference.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {pathToFileURL} from 'node:url';
const [sourceArg,outputArg]=process.argv.slice(2);
if(!sourceArg||!outputArg||process.versions.node!=='22.22.3')throw Error('Pinned Node 22.22.3, SOURCE and fresh OUTPUT required');
const source=path.resolve(sourceArg),output=path.resolve(outputArg);
const commit='1438b1abfcaee3267092b75573014f4d9b4a983c';
const git=(...args)=>execFileSync('git',['-C',source,...args],{encoding:'utf8'}).trim();
if(git('rev-parse','HEAD')!==commit||git('status','--porcelain','--untracked-files=no'))throw Error('Intact pinned reference required');
const rel=path.relative(source,output);
if(fs.existsSync(output)||!rel||(!rel.startsWith('..')&&!path.isAbsolute(rel)))throw Error('Fresh external output required');
const at=name=>import(pathToFileURL(path.join(source,name)).href);
await at('test/headless.mjs');
const {GPU}=await at('src/engine/gpu/GPU.js');
await GPU.init({headless:true});
const {ReefBatch}=await at('src/world/reef/ReefBatch.js');
const {createPropMaterial,skinTable,FISH_SPECULAR}=await at('src/world/fish/FishMaterial.js');
const {collectModules}=await at('src/engine/gpu/Shader.js');
const {PATTERN,SPECIES,SKIN}=await at('src/world/fish/FishSpecies.js');
const {PART,fishGeometry}=await at('src/world/fish/FishGeometry.js');
const geometry=fishGeometry(SPECIES.tuna,{lod:1,pose:'dead'});
const batch=new ReefBatch('FishProps',[{geometry}],{maxInstances:1,fade:true});
const main=createPropMaterial(batch),fade=createPropMaterial(batch,{fade:true});
const table=skinTable();table._pack();
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
fs.mkdirSync(output);
const files={};
const write=(name,data)=>{const bytes=Buffer.isBuffer(data)?data:Buffer.from(data);fs.writeFileSync(path.join(output,name),bytes,{flag:'wx'});files[name]={bytes:bytes.length,sha256:hash(bytes)};return name;};
write('skin-table-f32.bin',Buffer.from(table.data));
const materials={};
for(const [name,material] of Object.entries({main,fade})){
 for(const field of ['vertex','surface','shadow'])write(`${name}-${field}.wgsl`,material[field]);
 materials[name]={name:material.name,attributes:material.attributes,varyings:material.varyings,defines:material.defines,side:material.side,transparent:material.transparent,depth_write:material.depthWrite,roughness:material.uniforms.roughness.value,metalness:material.uniforms.metalness.value};
}
const modules=collectModules(main.modules).map((module,i)=>{
 const bindings=Object.fromEntries(Object.entries(module.bindings).map(([name,b])=>[name,b.uniform?{uniform_struct:b.uniform.structName,byte_length:b.uniform.byteLength}:b.storage?{storage:true,access:b.access??'read'}:{other:true}]));
 return {name:module.name,code:write(`module-${i}-${module.name}.wgsl`,module.code),dependencies:module.deps.map(d=>d.name),bindings};
});
const names=['LICENSE','src/world/fish/FishMaterial.js','src/world/fish/FishSpecies.js','src/world/fish/FishGeometry.js','src/world/reef/ReefBatch.js','src/engine/gpu/Uniforms.js','src/engine/gpu/Shader.js','src/engine/render/Material.js','src/engine/render/wgsl/common.js','src/materials/LODFade.js'];
const provenance=Object.fromEntries(names.map((name,i)=>[name,write(`source-${i}-${path.basename(name)}`,fs.readFileSync(path.join(source,name)))]));
const descriptor={schema:'forge.tidewater.fish-material-source',version:1,source_commit:commit,node_version:process.versions.node,specular_mode:FISH_SPECULAR,materials,modules,skin_table:{file:'skin-table-f32.bin',struct:table.structName,byte_length:table.byteLength,rows:table.fields.rows.value.map(v=>[v.x,v.y,v.z,v.w])},pattern:PATTERN,parts:PART,species:SPECIES,skin:SKIN,provenance,files,limits:['Generated source contract only; Rust shader adaptation and visual parity unverified','Dummy geometry is used solely to instantiate the unchanged source material factories; no source placements are exported here']};
fs.writeFileSync(path.join(output,'fish-material.json'),JSON.stringify(descriptor,null,2),{flag:'wx'});
if(git('status','--porcelain','--untracked-files=no'))throw Error('Reference changed');
console.log(JSON.stringify({files:Object.keys(files).length,modules:modules.length,skin_bytes:table.byteLength,specular:FISH_SPECULAR,source_unchanged:true}));
GPU.device.destroy();process.exit(0);