// Independent pinned-source CPU oracle. No Forge-selected nodes are inputs.
import {readFileSync,writeFileSync,existsSync} from 'node:fs';
import {execFileSync} from 'node:child_process';
import {resolve,join,relative,isAbsolute} from 'node:path';
import {pathToFileURL} from 'node:url';
import {createHash} from 'node:crypto';
const [sourceArg,baselineArg,outputArg]=process.argv.slice(2);
if(!sourceArg||!baselineArg||!outputArg) throw new Error('SOURCE BASELINE NEW_JSON required');
const source=resolve(sourceArg),baseline=resolve(baselineArg),output=resolve(outputArg);
const rel=relative(source,output);
if(!rel||(!rel.startsWith('..')&&!isAbsolute(rel))||existsSync(output)) throw new Error('New external output required');
const PIN='1438b1abfcaee3267092b75573014f4d9b4a983c';
const git=(...args)=>execFileSync('git',['-C',source,...args],{encoding:'utf8'}).trim();
if(git('rev-parse','HEAD')!==PIN||git('status','--porcelain')) throw new Error('Source must be clean and pinned');
const moduleAt=p=>import(pathToFileURL(join(source,p)).href);
await moduleAt('test/headless.mjs');
const {GPU}=await moduleAt('src/engine/gpu/GPU.js');await GPU.init({headless:true});
const {TerrainData}=await moduleAt('src/world/TerrainData.js');
const {Village}=await moduleAt('src/world/Village.js');
const {Colliders}=await moduleAt('src/world/Colliders.js');
const {Scene,PerspectiveCamera}=await moduleAt('src/engine/index.js');
const {CDLOD}=await moduleAt('src/core/CDLOD.js');
const terrain=new TerrainData();new Village({scene:new Scene(),terrain,colliders:new Colliders()});
const probes=new Map();
const heightBounds=(x,z,x1,z1)=>{
  if(x1-x!==z1-z) throw new Error('Square node expected');
  const bounds=terrain.boundsFor(x,z,x1,z1);
  probes.set([x,z,x1-x].join(','),{origin:[x,z],size:x1-x,bounds});return bounds;
};
const lod=new CDLOD({gridSize:40,leafSize:8,levels:9,rangeFactor:2,
  center:{x:-1024,z:-1024,size:2048},heightBounds,prefix:'terrainLod'});
const cases=[];
function sample(name,position,quaternion,fov=62){
  const camera=new PerspectiveCamera(fov,1920/1080,.06,60000);
  camera.position.set(...position);camera.quaternion.set(...quaternion);camera.updateMatrixWorld();
  lod.update(camera);
  if(lod.count>=lod.maxInstances) throw new Error('Source capacity may have truncated nodes');
  const nodes=[];
  for(let i=0;i<lod.count;i++) nodes.push(Array.from(lod.nodeArray.slice(i*4,i*4+4)));
  cases.push({name,position,planes:lod._frustum.planes.map(p=>[p.normal.x,p.normal.y,p.normal.z,p.constant]),nodes});
}
for(const name of ['coast','village','island','reef','canopy','coast-night']) {
  const reference=JSON.parse(readFileSync(join(baseline,`${name}.json`),'utf8'));
  if(reference.source_commit!==PIN) throw new Error('Wrong baseline source');
  const c=reference.camera;
  sample(name,['x','y','z'].map(k=>c.position[k]),['_x','_y','_z','_w'].map(k=>c.quaternion[k]),c.fov);
}
const coast=JSON.parse(readFileSync(join(baseline,'coast.json'),'utf8')).camera;
for(let i=0;i<41;i++) sample(`coast-track-${i}`,
  [coast.position.x+(i-20)*1.25,coast.position.y,coast.position.z],
  ['_x','_y','_z','_w'].map(k=>coast.quaternion[k]));
const hash=bytes=>createHash('sha256').update(bytes).digest('hex');
const heights=Buffer.from(terrain.heights.buffer,terrain.heights.byteOffset,terrain.heights.byteLength);
writeFileSync(output,JSON.stringify({schema:'forge.tidewater.cdlod-oracle',version:1,source_commit:PIN,
  heights_sha256:hash(heights),exporter_sha256:hash(readFileSync(new URL(import.meta.url))),
  config:{grid_size:40,leaf_size_m:8,levels:9,range_factor:2,morph_start_ratio:.66,max_nodes:1500},
  // Preserve exact binary64 values independently of a JSON parser's rounding.
  morph_f64_bits:lod.uMorph.array.map((v,i)=>[v.x,v.y,lod.uSpacing.array[i],0].map(n=>{
    const b=Buffer.alloc(8);b.writeDoubleLE(n);return b.readBigUInt64LE().toString(16).padStart(16,'0');
  })),
  bounds_probes:[...probes.values()],cases},null,2),{flag:'wx'});
if(git('status','--porcelain')) throw new Error('Source changed');
console.log(JSON.stringify({output,cases:cases.length,bounds_probes:probes.size,
  node_counts:cases.map(c=>[c.name,c.nodes.length]),status:'source_cpu_selection_oracle_only'}));
GPU.device.destroy();process.exit(0);
