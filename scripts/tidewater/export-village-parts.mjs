// Observe building emissions without changing generator bodies, random calls or merged output.
import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
import {execFileSync} from 'node:child_process';
import crypto from 'node:crypto';
const [sourceArg,outputArg]=process.argv.slice(2);
if(!sourceArg||!outputArg) throw Error('SOURCE NEW_DIRECTORY required');
const source=path.resolve(sourceArg),output=path.resolve(outputArg);
const git=(...args)=>execFileSync('git',['-C',source,...args],{encoding:'utf8'}).trim();
const pin='1438b1abfcaee3267092b75573014f4d9b4a983c';
if(git('rev-parse','HEAD')!==pin||git('status','--porcelain')) throw Error('clean pinned source required');
const relative=path.relative(source,output);
if(fs.existsSync(output)||!relative||(!relative.startsWith('..')&&!path.isAbsolute(relative))) throw Error('new external output required');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const at=p=>import(pathToFileURL(path.join(source,p)).href);
await at('test/headless.mjs');
const {GPU}=await at('src/engine/gpu/GPU.js');
await GPU.init({headless:true});
const {Scene}=await at('src/engine/index.js');
const {TerrainData}=await at('src/world/TerrainData.js');
const {Colliders}=await at('src/world/Colliders.js');
const {Village:OriginalVillage}=await at('src/world/Village.js');
const build=Type=>{const terrain=new TerrainData(),colliders=new Colliders();return {terrain,colliders,village:new Type({scene:new Scene(),terrain,colliders})};};
const baseline=build(OriginalVillage);
const original=fs.readFileSync(path.join(source,'src/world/Village.js'),'utf8');
const importLine="import { buildHouse, buildBoathouse, buildMarketStall, buildShed } from './village/Buildings.js';";
if(!original.includes(importLine)) throw Error('building imports changed');
const names=['buildHouse','buildBoathouse','buildMarketStall','buildShed'];
let code=original.replace(importLine,`import { ${names.map(n=>`${n} as original_${n}`).join(', ')} } from './village/Buildings.js';`);
code=code.replace(/from\s+(['"])(\.[^'"]+)\1/g,(_,q,p)=>`from ${JSON.stringify(pathToFileURL(path.resolve(source,'src/world',p)).href)}`);
const prelude=`
export const capturedBuildings=[];
const widths={pos:3,nrm:3,uv:2,tint:3,data:4};
function observe(kind,fn,ctx,spec){
 const before=Object.fromEntries(Object.entries(ctx.B.batches).map(([k,b])=>[k,{vertices:b.vcount,indices:b.idx.length}]));
 const result=fn(ctx,spec);
 const parts={};
 for(const [material,b] of Object.entries(ctx.B.batches)){
  const start=before[material]??{vertices:0,indices:0};
  if(b.vcount===start.vertices)continue;
  const attributes={};
  for(const [name,width] of Object.entries(widths)) attributes[name]=new Float32Array(b[name].slice(start.vertices*width));
  const indices=new Uint32Array(b.idx.slice(start.indices).map(i=>i-start.vertices));
  if(indices.some(i=>i>=b.vcount-start.vertices))throw Error('building refers outside emitted vertices');
  parts[material]={start,vertices:b.vcount-start.vertices,attributes,indices,sourceVdata:new Float64Array(b.data.slice(start.vertices*4))};
 }
 capturedBuildings.push({kind,spec:JSON.parse(JSON.stringify(spec)),result:JSON.parse(JSON.stringify(result)),parts});
 return result;
}
${names.map(n=>`const ${n}=(ctx,spec)=>observe('${n}',original_${n},ctx,spec);`).join('\n')}
`;
code=prelude+code;
const observed=await import('data:text/javascript;base64,'+Buffer.from(code).toString('base64'));
let assemblyBatches;
const assemble=observed.Village.prototype._assemble;
observed.Village.prototype._assemble=function(...args){
 assemblyBatches=Object.fromEntries(Object.entries(this.B.batches).map(([key,b])=>[key,{vertices:b.vcount,indices:b.idx.length}]));
 return assemble.apply(this,args);
};
const instrumented=build(observed.Village);
function digest({terrain,village}){
 village.group.updateMatrixWorld(true);
 const nodes=[];
 village.group.traverse(n=>{
  if(!n.geometry)return;
  const g=n.geometry;
  const attributes=Object.fromEntries(Object.entries(g.attributes).map(([k,a])=>{
   const data=a.isInterleavedBufferAttribute?a.data.array:a.array;
   return [k,{hash:hash(Buffer.from(data.buffer,data.byteOffset,data.byteLength)),count:a.count,itemSize:a.itemSize}];
  }));
  const idx=g.index?.array;
  nodes.push({name:n.name,matrix:Array.from(n.matrixWorld.elements),attributes,indices:idx?hash(Buffer.from(idx.buffer,idx.byteOffset,idx.byteLength)):null,drawRange:g.drawRange});
 });
 return {terrain:hash(Buffer.from(terrain.heights.buffer)),nodes,buildings:village.buildings,footprints:village.footprints};
}
const a=digest(baseline),b=digest(instrumented);
if(JSON.stringify(a)!==JSON.stringify(b)) throw Error('observation changed generated village');
if(a.terrain!=='be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3') throw Error('terrain differs from acquisition');
fs.mkdirSync(output);
const write=(name,bytes)=>{fs.writeFileSync(path.join(output,name),bytes,{flag:'wx'});return {file:name,bytes:bytes.length,sha256:hash(bytes)};};
const records=observed.capturedBuildings.map((record,i)=>{
 const parts={};
 for(const [material,part] of Object.entries(record.parts)){
  const attributes=Object.fromEntries(Object.entries(part.attributes).map(([name,values])=>[name,write(`${i}-${material}-${name}.bin`,Buffer.from(values.buffer))]));
  parts[material]={start:part.start,vertices:part.vertices,attributes,indices:write(`${i}-${material}-indices.bin`,Buffer.from(part.indices.buffer)),source_vdata:write(`${i}-${material}-source-vdata.f64.bin`,Buffer.from(part.sourceVdata.buffer))};
 }
 return {source_id:`tidewater:${pin}:building:${i}`,kind:record.kind,spec:record.spec,result:record.result,parts};
});
const report={schema:'forge.tidewater.building-parts',version:3,assembly_batches:assemblyBatches,source_commit:pin,source_file_sha256:hash(original),observed_module_sha256:hash(code),
 unchanged_merged_scene:true,terrain_sha256:a.terrain,scene_digest:hash(JSON.stringify(a)),buildings:records,
 limits:['world-space pieces before material folding','building-attached static props included','dynamic objects and later village props not yet separated','not yet a Forge catalog or Studio import']};
write('buildings.json',Buffer.from(JSON.stringify(report,null,2)));
if(git('status','--porcelain'))throw Error('source was modified');
console.log(JSON.stringify({buildings:records.length,parts:records.reduce((s,b)=>s+Object.keys(b.parts).length,0),scene_digest:report.scene_digest,unchanged_merged_scene:true}));
GPU.device.destroy();
process.exit(0);
