// Original coupled FFT, ShoreSim, breaker emissions and spray; no clouds or CPU emitters.
import {readFileSync,writeFileSync,mkdirSync,existsSync} from 'node:fs';
import {resolve,join,relative,isAbsolute} from 'node:path';
import {pathToFileURL} from 'node:url';
import {execFileSync} from 'node:child_process';
import {createHash} from 'node:crypto';
const [sourceArg,outputArg,windOption]=process.argv.slice(2);
const sceneWind=windOption==='--scene-wind';
if(!outputArg||![4,5].includes(process.argv.length)||(windOption&&!sceneWind))throw Error('SOURCE NEW_DIRECTORY [--scene-wind] required');
const source=resolve(sourceArg),output=resolve(outputArg),pin='1438b1abfcaee3267092b75573014f4d9b4a983c';
const git=(...a)=>execFileSync('git',['-C',source,...a],{encoding:'utf8'}).trim(),rel=relative(source,output);
if(git('rev-parse','HEAD')!==pin||git('status','--porcelain'))throw Error('Clean pinned source required');
if(existsSync(output)||!rel||(!rel.startsWith('..')&&!isAbsolute(rel)))throw Error('New external output required');
const at=p=>import(pathToFileURL(join(source,p)).href);
await at('test/headless.mjs');
const {GPU}=await at('src/engine/gpu/GPU.js');await GPU.init({headless:true});GPU.device.pushErrorScope('validation');
const {Scene,Vector3}=await at('src/engine/index.js');
const {TerrainData}=await at('src/world/TerrainData.js');
const {TerrainGPU}=await at('src/world/TerrainGPU.js');
const {Village}=await at('src/world/Village.js');
const {Colliders}=await at('src/world/Colliders.js');
const {computeShoreField}=await at('src/world/ShoreField.js');
const {WORLD}=await at('src/world/WorldLayout.js');
const {ShoreWaves}=await at('src/ocean/ShoreWaves.js');
const {OceanFFT}=await at('src/ocean/OceanFFT.js');
const {WaterSurface}=await at('src/ocean/WaterSurface.js');
const {Breakers}=await at('src/ocean/Breakers.js');
const {ShoreSim}=await at('src/ocean/ShoreSim.js');
const {WaterQuery}=await at('src/ocean/WaterQuery.js');
const {Spray}=await at('src/fx/Spray.js');
const {RenderTarget,StorageBuffer,Texture}=await at('src/engine/gpu/Texture.js');
const {makeLaceTexture}=await at('src/ocean/SurfFoam.js');
const {CDLOD}=await at('src/core/CDLOD.js');
const {Sky}=await at('src/sky/Sky.js');
const {Atmosphere}=await at('src/sky/Atmosphere.js');
const {G,FrameUniforms}=await at('src/engine/render/Frame.js');
const {readBuffer,readTexture}=await at('src/engine/gpu/Readback.js');
const {collectModules}=await at('src/engine/gpu/Shader.js');
const terrain=new TerrainData();new Village({scene:new Scene(),terrain,colliders:new Colliders()});
const field=computeShoreField(terrain,{res:512,swellDir:[WORLD.swellDir.x,WORLD.swellDir.y]});
const terrainGPU=new TerrainGPU(terrain,field),shore=new ShoreWaves(terrainGPU),fft=new OceanFFT(null);
const surface=new WaterSurface({fft,cdlod:new CDLOD({gridSize:32,leafSize:8,levels:12,minY:-25,maxY:25})});
surface.terrain=terrainGPU;surface.shore=shore;
const sim=new ShoreSim(null,{terrainGPU,shore});surface.shoreSim=sim;
const query=new WaterQuery(null,surface);
const sceneCopy=new RenderTarget(1920,1080,{colors:['rgba8unorm-srgb'],depth:'depth32float'});
// Readback-only usage instrumentation before the original constructor allocates textures.
const originalGetGPU=Texture.prototype.getGPU;
Texture.prototype.getGPU=function(...args){
 if(['sprayPuff','sprayDots'].includes(this.label)&&!this.usageList.includes('copySrc'))this.usageList=[...this.usageList,'copySrc'];
 return originalGetGPU.apply(this,args);
};
let spray;
try{spray=new Spray(null,{query,terrain:terrainGPU,sceneCopy});}finally{Texture.prototype.getGPU=originalGetGPU;}
const breakers=new Breakers(null,{surface,shore,terrainData:terrain,sky:new Sky(new Atmosphere()),spray});
if(breakers.NS<=1||shore.uniforms.fields.dirOn.value!==0)throw Error('Unexpected beach or shore fallback');
const bytes=a=>Buffer.from(new Uint8Array(a.buffer,a.byteOffset,a.byteLength));
const payloads={'heights.f32.bin':bytes(terrain.heights),'stations.f32.bin':bytes(breakers.stationData),
 'shore-field.rgba32.bin':bytes(field.data),'kernel.wgsl':Buffer.from(breakers.kernel.source),
 'lip-vertex.wgsl':Buffer.from(breakers.material.vertex),'lip-output.wgsl':Buffer.from(breakers.material.output),
 'sheet-ids.f32.bin':bytes(breakers.mesh.geometry.getAttribute('sheetId').array),
 'indices.u32.bin':bytes(breakers.mesh.geometry.getIndex().array)};
for(const [i,mip] of makeLaceTexture().userData.mips.entries())payloads[`lace-m${i}.rgba8.bin`]=bytes(mip.data);
for(const m of collectModules(breakers.material.modules))payloads[`module-${m.name}.wgsl`]=Buffer.from(m.code);
for(const [name,block] of [['shore',shore.uniforms],['terrain',terrainGPU.uniforms],['breakers',breakers.uniforms],['material',breakers.material.uniformBlock]]){
 block._pack();payloads[name+'-params.bin']=bytes(new Uint8Array(block.data));payloads[name+'-params.wgsl']=Buffer.from(block.wgsl);
}
const emitted=Object.fromEntries(['pos','vel','info','head'].map(n=>[n,new StorageBuffer({label:'emission snapshot '+n,count:n==='head'?1:spray.N,type:n==='head'?'u32':'vec4f'})]));
const headHistory=new StorageBuffer({label:'head history',count:540,type:'u32'});
const snapshots=[],schedule=[],camera={position:new Vector3(42,14,-90)};
if(sceneWind){G.windSpeed.value=6;G.windDir.value.set(1,0);}
G.time.value=0;G.dt.value=0;G.seaLevel.value=0;GPU.submit();await GPU.queue.onSubmittedWorkDone();
let time=0;
for(let frame=1;frame<=540;frame++){
 time+=1/60;GPU.beginFrame();G.time.value=time;G.dt.value=1/60;FrameUniforms.fields.frameIndex.value=frame;
 fft.update(1/60);sim.update();
 const seedBefore=spray.frameSeed.value;
 const emitterBudget=breakers.uniforms.fields.budget.value;
 breakers.update(camera);
 if([1,120,540].includes(frame)){
  for(const name of ['pos','vel','info','head'])GPU.getEncoder().copyBufferToBuffer(spray[name].getGPU(),0,emitted[name].getGPU(),0,name==='head'?4:spray.N*16);
 }
 spray.update();
 GPU.getEncoder().copyBufferToBuffer(spray.head.getGPU(),0,headHistory.getGPU(),(frame-1)*4,4);
 schedule.push({frame,time,emitter_seed:seedBefore,emitter_budget:emitterBudget,update_seed:spray.frameSeed.value,effective_gpu_seed:spray.frameSeed.value,budget:breakers.uniforms.fields.budget.value});
 GPU.submit();await GPU.queue.onSubmittedWorkDone();
 if([1,120,540].includes(frame))for(const name of ['pos','vel','info','head'])payloads[`emitted-${String(frame).padStart(4,'0')}-${name}.bin`]=Buffer.from(await readBuffer(emitted[name],name==='head'?4:spray.N*16));
 if([1,120,540].includes(frame)){
  const prefix=`frame-${String(frame).padStart(4,'0')}`;
  FrameUniforms._pack();breakers.uniforms._pack();fft.params._pack();
  payloads[prefix+'-frame.bin']=bytes(new Uint8Array(FrameUniforms.data));
  payloads[prefix+'-params.bin']=bytes(new Uint8Array(breakers.uniforms.data));
  payloads[prefix+'-ocean.bin']=bytes(new Uint8Array(fft.params.data));
  payloads[prefix+'-crests.f32.bin']=Buffer.from(await readBuffer(breakers.crest,breakers.NS*6*16));
  // Final FFT inputs permit isolated crest-kernel qualification before graph integration.
  for(let mip=0;mip<fft.displacementTexture.mipLevelCount;mip++){
   const layers=[];for(let layer=0;layer<4;layer++)layers.push(Buffer.from((await readTexture(fft.displacementTexture,{mip,layer})).data));
   payloads[`${prefix}-displacement-m${mip}.rgba16.bin`]=Buffer.concat(layers);
  }
  for(const [name,buffer,size] of [['pos',spray.pos,spray.N*16],['vel',spray.vel,spray.N*16],['info',spray.info,spray.N*16],['head',spray.head,4],['deposit',sim.deposit,sim.res*sim.res*4]])payloads[prefix+'-'+name+'.bin']=Buffer.from(await readBuffer(buffer,size));
  payloads[prefix+'-shore.rgba16.bin']=Buffer.from((await readTexture(sim.stateA)).data);
  spray.simParams._pack();payloads[prefix+'-spray-params.bin']=bytes(new Uint8Array(spray.simParams.data));
  snapshots.push({frame,time,prefix});
 }
}
payloads['history-head.u32.bin']=Buffer.from(await readBuffer(headHistory,540*4));
payloads['spray-update.wgsl']=Buffer.from(spray.updateKernel.source);
payloads['shore-update.wgsl']=Buffer.from(sim.kernel.source);
for(const [name,block] of [['spray',spray.simParams],['shore-sim',sim.uniforms],['surface',surface.params]]){block._pack();payloads[name+'-params.bin']=bytes(new Uint8Array(block.data));payloads[name+'-params.wgsl']=Buffer.from(block.wgsl);}
// Resolve the same lazy lighting hooks as the first draw, then acquire every sprite input.
spray.material.pipelineKey();
payloads['sprite-vertex.wgsl']=Buffer.from(spray.material.vertex);
payloads['sprite-output.wgsl']=Buffer.from(spray.material.output);
spray.material.uniformBlock._pack();
payloads['sprite-material-params.bin']=bytes(new Uint8Array(spray.material.uniformBlock.data));
payloads['sprite-material-params.wgsl']=Buffer.from(spray.material.uniformBlock.wgsl);
const spriteModules=collectModules(spray.material.modules);
for(const m of spriteModules)payloads[`sprite-module-${m.name}.wgsl`]=Buffer.from(m.code);
const spriteTextures={};
for(const name of ['sprayPuff','sprayDots']){
 const texture=spray.material.bindings[name].texture;
 spriteTextures[name]={width:texture.width,height:texture.height,format:texture.format,mips:texture.mipLevelCount};
 for(let mip=0;mip<texture.mipLevelCount;mip++)payloads[`sprite-${name}-m${mip}.rgba8.bin`]=Buffer.from((await readTexture(texture,{mip})).data);
}
const error=await GPU.device.popErrorScope();if(error)throw Error(error.message);
if(git('status','--porcelain'))throw Error('Source changed');
const hash=b=>createHash('sha256').update(b).digest('hex'),files={};mkdirSync(output,{recursive:true});
for(const [name,data] of Object.entries(payloads)){writeFileSync(join(output,name),data,{flag:'wx'});files[name]=hash(data);}
writeFileSync(join(output,'spray.json'),JSON.stringify({schema:'forge.tidewater.spray',version:1,source_commit:pin,
 stations:breakers.NS,spacing:breakers.spacing,profile_vertices:20,slots:2,terrain:{resolution:terrain.res,origin:terrain.origin,texel:terrain.texel},
 capacities:{gpu:spray.NG,cpu:spray.NC,total:spray.N},schedule,update_bindings:spray.updateKernel.bindings.names.map((name,i)=>({name,binding:i,layout:spray.updateKernel.bindings.described[i].layout,decl:spray.updateKernel.bindings.described[i].decl})),
 terrain_sha256:files['heights.f32.bin'],snapshots,bindings:breakers.kernel.bindings.names.map((name,i)=>({name,binding:i,layout:breakers.kernel.bindings.described[i].layout,decl:breakers.kernel.bindings.described[i].decl})),
 global_wind:{speed:G.windSpeed.value,direction:[G.windDir.value.x,G.windDir.value.y]},
 sprite:{modules:spriteModules.map(m=>m.name),textures:spriteTextures,blend:'premultiplied',depth_test:true,depth_write:false,capacity:spray.N},
 options:{spray:true,clouds:false,cpu_emitters:false,body:false,shore_deposit:true},files,exporter_sha256:hash(readFileSync(new URL(import.meta.url))),
 limitations:['source_coupled_compute_only','atomic_ring_slot_order_may_differ','async_budget_schedule_recorded','no_native_spray_or_visual_verdict','cpu_emitters_body_clouds_disabled']},null,2),{flag:'wx'});
console.log(JSON.stringify({output,stations:breakers.NS,files:Object.keys(files).length,snapshots}));GPU.device.destroy();process.exit(0);

