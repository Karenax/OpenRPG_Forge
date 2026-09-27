// Exact ocean CDLOD selection and base grid from the pinned engine.
import {readFileSync,writeFileSync,existsSync,mkdirSync} from 'node:fs';
import {execFileSync} from 'node:child_process';
import {resolve,join,relative,isAbsolute} from 'node:path';
import {pathToFileURL} from 'node:url';
import {createHash} from 'node:crypto';
const [sourceArg,baselineArg,outputArg]=process.argv.slice(2);
if(!outputArg||process.argv.length!==5)throw Error('SOURCE BASELINE NEW_DIRECTORY required');
const source=resolve(sourceArg),baseline=resolve(baselineArg),output=resolve(outputArg);
const rel=relative(source,output),pin='1438b1abfcaee3267092b75573014f4d9b4a983c';
if(!rel||(!rel.startsWith('..')&&!isAbsolute(rel))||existsSync(output))throw Error('New external output required');
const git=(...args)=>execFileSync('git',['-C',source,...args],{encoding:'utf8'}).trim();
if(git('rev-parse','HEAD')!==pin||git('status','--porcelain'))throw Error('Clean pinned source required');
const at=p=>import(pathToFileURL(join(source,p)).href);
await at('test/headless.mjs');
const {GPU}=await at('src/engine/gpu/GPU.js');
await GPU.init({headless:true});
const {PerspectiveCamera}=await at('src/engine/index.js');
const {CDLOD}=await at('src/core/CDLOD.js');
const {OceanFFT}=await at('src/ocean/OceanFFT.js');
const {WaterSurface}=await at('src/ocean/WaterSurface.js');
const lod=new CDLOD({gridSize:32,leafSize:8,levels:12,minY:-25,maxY:25});
const cases=[];
function sample(name,position,q,fov=62){
 const c=new PerspectiveCamera(fov,1920/1080,.06,60000);
 c.position.set(...position);c.quaternion.set(...q);c.updateMatrixWorld();lod.update(c);
 if(lod.count>=lod.maxInstances)throw Error('Possible truncated node selection');
 const nodes=Array.from({length:lod.count},(_,i)=>Array.from(lod.nodeArray.slice(i*4,i*4+4)));
 cases.push({name,position,quaternion:q,fov,planes:lod._frustum.planes.map(p=>[p.normal.x,p.normal.y,p.normal.z,p.constant]),nodes});
}
for(const name of ['coast','village','island','reef','canopy','coast-night']){
 const ref=JSON.parse(readFileSync(join(baseline,name+'.json')));
 if(ref.source_commit!==pin)throw Error('Wrong baseline');
 const c=ref.camera;sample(name,['x','y','z'].map(k=>c.position[k]),['_x','_y','_z','_w'].map(k=>c.quaternion[k]),c.fov);
}
const coast=cases[0];
for(let i=0;i<41;i++)sample('coast-track-'+i,[coast.position[0]+(i-20)*1.25,coast.position[1],coast.position[2]],coast.quaternion,coast.fov);
for(const axis of [0,2])for(const boundary of [-16384,0,16384])for(const delta of [-.001,0,.001]){
 const p=[...coast.position];p[axis]=boundary+delta;
 sample(`root-${axis}-${boundary}-${delta}`,p,coast.quaternion,coast.fov);
}
for(const y of [-30,-1,0,25,1000])sample('height-'+y,[coast.position[0],y,coast.position[2]],coast.quaternion,coast.fov);
const payloads={
 'grid.f32.bin':Buffer.from(lod.geometry.getAttribute('position').array.buffer),
 'indices.u32.bin':Buffer.from(lod.geometry.index.array.buffer),
 'cdlod.wgsl':Buffer.from(lod.module.code),
};
// Extract the original terrain-aware vertex composition without enabling
// shore/wake/detail modules yet. No substitute wave function is introduced.
const fft=new OceanFFT(null);
const surface=new WaterSurface({fft,cdlod:lod,foamTexture:null});
surface.terrain={module:null};
const surfaceCode=surface.module.code;
// Retain the complete composed module for provenance and later material work.
const fragmentStart=surfaceCode.indexOf('struct WaterSurfaceFrag');
if(fragmentStart<0||!surfaceCode.includes('frame.seaLevel - terrainHeightAt( xz )'))throw Error('Surface source structure changed');
payloads['surface-module.wgsl']=Buffer.from(surfaceCode);
payloads['surface-vertex.wgsl']=Buffer.from(surfaceCode.slice(0,fragmentStart));
payloads['surface-attenuation.wgsl']=Buffer.from(surface.attenuationModule.code);
surface.params._pack();payloads['surface-params.bin']=Buffer.from(surface.params.data);
lod.params._pack();payloads['morph.bin']=Buffer.from(lod.params.data);
const bits=n=>{const b=Buffer.alloc(8);b.writeDoubleLE(n);return b.readBigUInt64LE().toString(16).padStart(16,'0');};
const hash=b=>createHash('sha256').update(b).digest('hex');
if(git('status','--porcelain'))throw Error('Source changed');
mkdirSync(output,{recursive:true});const files={};
for(const [name,data] of Object.entries(payloads)){writeFileSync(join(output,name),data,{flag:'wx'});files[name]=hash(data);}
writeFileSync(join(output,'ocean-lod.json'),JSON.stringify({schema:'forge.tidewater.ocean-lod',version:1,source_commit:pin,
 config:{grid_size:32,leaf_size_m:8,levels:12,range_factor:2.5,morph_start_ratio:.66,max_nodes:1500},
 bounds:[-25,25],surface_options:{terrain:true,shore:false,wake:false,detail:false,shore_sim:false,surf_foam:false},
 morph_f64_bits:lod.uMorph.array.map((v,i)=>[v.x,v.y,lod.uSpacing.array[i],0].map(bits)),
 cases,files,exporter_sha256:hash(readFileSync(new URL(import.meta.url))),limitations:['cpu_selection_and_grid_only','no_surface_displacement_or_water_material_verdict']},null,2),{flag:'wx'});
console.log(JSON.stringify({output,cases:cases.length,ordered_nodes:cases.reduce((n,c)=>n+c.nodes.length,0),baseline:cases.slice(0,6).map(c=>[c.name,c.nodes.length])}));
GPU.device.destroy();process.exit(0);
