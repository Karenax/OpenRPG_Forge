// Preserve the original optical material and procedural foam, without patching source.
import {readFileSync,writeFileSync,mkdirSync,existsSync} from 'node:fs';
import {resolve,join,relative,isAbsolute} from 'node:path';
import {pathToFileURL} from 'node:url';
import {execFileSync} from 'node:child_process';
import {createHash} from 'node:crypto';
import {terrainWetnessHook} from './shore-reference-hooks.mjs';
const [sourceArg,outputArg,shoreOption]=process.argv.slice(2);
if(!outputArg||process.argv.length>5||(shoreOption&&!['--shore','--shore-sim','--shore-detail'].includes(shoreOption)))throw Error('SOURCE NEW_DIRECTORY [--shore|--shore-sim|--shore-detail] required');
const withDetail=shoreOption==='--shore-detail',withSim=withDetail||shoreOption==='--shore-sim',withShore=!!shoreOption;
const source=resolve(sourceArg),output=resolve(outputArg),pin='1438b1abfcaee3267092b75573014f4d9b4a983c';
const git=(...a)=>execFileSync('git',['-C',source,...a],{encoding:'utf8'}).trim();
const rel=relative(source,output);
if(git('rev-parse','HEAD')!==pin||git('status','--porcelain'))throw Error('Clean pinned source required');
if(existsSync(output)||!rel||(!rel.startsWith('..')&&!isAbsolute(rel)))throw Error('New external output required');
const at=p=>import(pathToFileURL(join(source,p)).href);
await at('test/headless.mjs');
const {GPU}=await at('src/engine/gpu/GPU.js');
await GPU.init({headless:true});GPU.device.pushErrorScope('validation');
const {Texture,RenderTarget}=await at('src/engine/gpu/Texture.js');
const {ComputeKernel}=await at('src/engine/gpu/Compute.js');
const {collectModules}=await at('src/engine/gpu/Shader.js');
const {readTexture}=await at('src/engine/gpu/Readback.js');
const {OceanFFT}=await at('src/ocean/OceanFFT.js');
const {WaterSurface}=await at('src/ocean/WaterSurface.js');
const {WaterMaterial}=await at('src/ocean/WaterMaterial.js');
const {createFoamTexture}=await at('src/ocean/FoamTexture.js');
const {CDLOD}=await at('src/core/CDLOD.js');
const {Atmosphere}=await at('src/sky/Atmosphere.js');
const {Sky}=await at('src/sky/Sky.js');
const {TerrainData}=await at('src/world/TerrainData.js');
const {TerrainGPU}=await at('src/world/TerrainGPU.js');
const {FrameUniforms}=await at('src/engine/render/Frame.js');
const payloads={};
// COPY_SRC only changes acquisition permissions. The original kernel and mip
// renderer still generate every texel; no alternate pattern is substituted.
const create=Texture.prototype._create,dispatch=ComputeKernel.prototype.dispatch;
Texture.prototype._create=function(...a){
 if(this.label==='foamPattern'&&!this.usageList.includes('copySrc'))this.usageList.push('copySrc');
 return create.apply(this,a);
};
ComputeKernel.prototype.dispatch=function(...a){
 if(this.label==='Foam Pattern')payloads['foam-kernel.wgsl']=Buffer.from(this.source);
 return dispatch.apply(this,a);
};
let foam;
try{foam=createFoamTexture(null);}finally{Texture.prototype._create=create;ComputeKernel.prototype.dispatch=dispatch;}
GPU.submit();await GPU.queue.onSubmittedWorkDone();
for(let mip=0;mip<foam.mipLevelCount;mip++)
 payloads[`foam-m${mip}.rgba16.bin`]=Buffer.from((await readTexture(foam,{mip})).data);
const fft=new OceanFFT(null),lod=new CDLOD({gridSize:32,leafSize:8,levels:12,minY:-25,maxY:25});
const surface=new WaterSurface({fft,cdlod:lod,foamTexture:foam});
if(withDetail){
 const {SeaDetail}=await at('src/ocean/SeaDetail.js');surface.detail=new SeaDetail();
 const texture=surface.detail.texture;
 if(texture.width!==256||texture.height!==256||texture.format!=='rgba16float'||texture.mipLevelCount!==1)throw Error('Sea detail layout changed');
 payloads['sea-detail-noise.rgba16.bin']=Buffer.from(new Uint8Array(texture.pendingData.buffer,texture.pendingData.byteOffset,texture.pendingData.byteLength));
}
const terrain=new TerrainData();let shoreField=null;
if(withShore){
 const {Scene}=await at('src/engine/index.js');
 const {Village}=await at('src/world/Village.js');
 const {Colliders}=await at('src/world/Colliders.js');
 const {computeShoreField}=await at('src/world/ShoreField.js');
 const {WORLD}=await at('src/world/WorldLayout.js');
 new Village({scene:new Scene(),terrain,colliders:new Colliders()});
 shoreField=computeShoreField(terrain,{res:512,swellDir:[WORLD.swellDir.x,WORLD.swellDir.y]});
 payloads['shore-field.rgba32.bin']=Buffer.from(new Uint8Array(shoreField.data.buffer));
}
surface.terrain=new TerrainGPU(terrain,shoreField);
if(withShore){const {ShoreWaves}=await at('src/ocean/ShoreWaves.js');surface.shore=new ShoreWaves(surface.terrain);}
if(withSim){
 const {ShoreSim}=await at('src/ocean/ShoreSim.js');
 const {SurfFoam}=await at('src/ocean/SurfFoam.js');
 surface.shoreSim=new ShoreSim(null,{terrainGPU:surface.terrain,shore:surface.shore});
 const surfFoam=new SurfFoam({shoreSim:surface.shoreSim});surface.foamShading=args=>surfFoam.shading(args);
 if(surface.shore.dirTexture||surface.shore.uniforms.fields.dirOn.value!==0)throw Error('Source direction fallback changed');
 for(const [i,mip] of surface.shoreSim.lace.userData.mips.entries())payloads[`lace-m${i}.rgba8.bin`]=Buffer.from(mip.data);
 payloads['terrain-wetness-hook.wgsl']=Buffer.from(terrainWetnessHook(source));
}
const sky=new Sky(new Atmosphere());
const copy=new RenderTarget(1920,1080,{colors:['rgba16float'],depth:'depth32float'});
const material=new WaterMaterial({surface,sky,sceneCopy:copy});
material.pipelineKey();
payloads['water-vertex.wgsl']=Buffer.from(material.vertex);
payloads['water-output.wgsl']=Buffer.from(material.output);
const bindings={},modules=[];
for(const module of collectModules(material.modules)){
 if(!/^[a-zA-Z0-9_-]+$/.test(module.name))throw Error('Unsafe module name');
 const file=`module-${module.name}.wgsl`;
 if(payloads[file])throw Error('Duplicate module name');
 payloads[file]=Buffer.from(module.code);modules.push({name:module.name,file,deps:module.deps.map(d=>d.name)});
 for(const [name,binding] of Object.entries(module.bindings)){
  bindings[name]={kind:binding.uniform?'uniform':binding.storage?'storage':binding.texture?'texture':'other'};
  if(binding.uniform){
   binding.uniform._pack();
   payloads[`uniform-${name}.wgsl`]=Buffer.from(binding.uniform.wgsl);
   payloads[`uniform-${name}.bin`]=Buffer.from(new Uint8Array(binding.uniform.data));
  }
 }
}
for(const [name,block] of [['mat',material.uniformBlock],['frame',FrameUniforms]]){
 block._pack();payloads[`uniform-${name}.wgsl`]=Buffer.from(block.wgsl);
 payloads[`uniform-${name}.bin`]=Buffer.from(new Uint8Array(block.data));
}
GPU.submit();await GPU.queue.onSubmittedWorkDone();
const error=await GPU.device.popErrorScope();if(error)throw Error(error.message);
if(!payloads['foam-kernel.wgsl']||git('status','--porcelain'))throw Error('Incomplete acquisition or changed source');
const hash=b=>createHash('sha256').update(b).digest('hex');
mkdirSync(output,{recursive:true});const files={};
for(const [name,data] of Object.entries(payloads)){writeFileSync(join(output,name),data,{flag:'wx'});files[name]=hash(data);}
writeFileSync(join(output,'water-material.json'),JSON.stringify({schema:'forge.tidewater.water-material',version:1,source_commit:pin,
 options:{terrain:true,shore:withShore,wake:false,detail:withDetail,shore_sim:withSim,surf_foam:withSim,clouds:false,planar_reflection:false,separate_refraction:false,hull:false},
 terrain_sha256:hash(Buffer.from(new Uint8Array(terrain.heights.buffer))),shore_field:withShore?{res:512,origin:terrain.origin,size:terrain.size}:null,
 modules,bindings,defines:material.allDefines(),foam:{width:foam.width,height:foam.height,mips:foam.mipLevelCount,format:foam.format},files,
 exporter_sha256:hash(readFileSync(new URL(import.meta.url))),limitations:['shader_and_foam_acquisition_only','uniforms_are_constructor_defaults',...(withShore?['shore_field_acquired_not_native_regenerated']:['terrain_generated_without_village_not_a_scene_export']),'no_native_optical_render_verdict']},null,2),{flag:'wx'});
console.log(JSON.stringify({output,files:Object.keys(files).length,modules:modules.length,foam_mips:foam.mipLevelCount}));
GPU.device.destroy();process.exit(0);
