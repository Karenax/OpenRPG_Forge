// Original beach stations, crest compute and lip inputs; no spray/cloud qualification.
import {readFileSync,writeFileSync,mkdirSync,existsSync} from 'node:fs';
import {resolve,join,relative,isAbsolute} from 'node:path';
import {pathToFileURL} from 'node:url';
import {execFileSync} from 'node:child_process';
import {createHash} from 'node:crypto';
const [sourceArg,outputArg]=process.argv.slice(2);
if(!outputArg||process.argv.length!==4)throw Error('SOURCE NEW_DIRECTORY required');
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
const breakers=new Breakers(null,{surface,shore,terrainData:terrain,sky:new Sky(new Atmosphere())});
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
const snapshots=[],camera={position:new Vector3(42,14,-90)};
G.time.value=0;G.dt.value=0;G.seaLevel.value=0;GPU.submit();await GPU.queue.onSubmittedWorkDone();
let time=0;
for(let frame=1;frame<=540;frame++){
 time+=1/60;GPU.beginFrame();G.time.value=time;G.dt.value=1/60;FrameUniforms.fields.frameIndex.value=frame;
 fft.update(1/60);breakers.update(camera);GPU.submit();await GPU.queue.onSubmittedWorkDone();
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
  snapshots.push({frame,time,prefix});
 }
}
const error=await GPU.device.popErrorScope();if(error)throw Error(error.message);
if(git('status','--porcelain'))throw Error('Source changed');
const hash=b=>createHash('sha256').update(b).digest('hex'),files={};mkdirSync(output,{recursive:true});
for(const [name,data] of Object.entries(payloads)){writeFileSync(join(output,name),data,{flag:'wx'});files[name]=hash(data);}
writeFileSync(join(output,'breakers.json'),JSON.stringify({schema:'forge.tidewater.breakers',version:1,source_commit:pin,
 stations:breakers.NS,spacing:breakers.spacing,profile_vertices:20,slots:2,terrain:{resolution:terrain.res,origin:terrain.origin,texel:terrain.texel},
 terrain_sha256:files['heights.f32.bin'],snapshots,bindings:breakers.kernel.bindings.names.map((name,i)=>({name,binding:i,layout:breakers.kernel.bindings.described[i].layout,decl:breakers.kernel.bindings.described[i].decl})),
 options:{spray:false,clouds:false},files,exporter_sha256:hash(readFileSync(new URL(import.meta.url))),
 limitations:['original_numerical_and_material_inputs_only','no_native_crest_or_lip_render_verdict','spray_and_clouds_disabled']},null,2),{flag:'wx'});
console.log(JSON.stringify({output,stations:breakers.NS,files:Object.keys(files).length,snapshots}));GPU.device.destroy();process.exit(0);
