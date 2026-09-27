// Independent temporal oracle for the original beach simulation, at full resolution.
import {readFileSync,writeFileSync,mkdirSync,existsSync} from 'node:fs';
import {resolve,join,relative,isAbsolute} from 'node:path';
import {pathToFileURL} from 'node:url';
import {execFileSync} from 'node:child_process';
import {createHash} from 'node:crypto';
const [sourceArg,outputArg,mode]=process.argv.slice(2);
if(!outputArg||process.argv.length>5||(mode&&!['--phase-probe','--hash-probe'].includes(mode)))throw Error('SOURCE NEW_DIRECTORY [--phase-probe|--hash-probe] required');
const source=resolve(sourceArg),output=resolve(outputArg),pin='1438b1abfcaee3267092b75573014f4d9b4a983c';
const git=(...a)=>execFileSync('git',['-C',source,...a],{encoding:'utf8'}).trim();
const rel=relative(source,output);
if(git('rev-parse','HEAD')!==pin||git('status','--porcelain'))throw Error('Clean pinned source required');
if(existsSync(output)||!rel||(!rel.startsWith('..')&&!isAbsolute(rel)))throw Error('New external output required');
const at=p=>import(pathToFileURL(join(source,p)).href);
await at('test/headless.mjs');
const {GPU}=await at('src/engine/gpu/GPU.js');await GPU.init({headless:true});GPU.device.pushErrorScope('validation');
const {Scene}=await at('src/engine/index.js');
const {TerrainData}=await at('src/world/TerrainData.js');
const {TerrainGPU}=await at('src/world/TerrainGPU.js');
const {Village}=await at('src/world/Village.js');
const {Colliders}=await at('src/world/Colliders.js');
const {WORLD}=await at('src/world/WorldLayout.js');
const {computeShoreField}=await at('src/world/ShoreField.js');
const {ShoreWaves}=await at('src/ocean/ShoreWaves.js');
const {ShoreSim}=await at('src/ocean/ShoreSim.js');
const {G,FrameUniforms}=await at('src/engine/render/Frame.js');
const {readTexture,readBuffer}=await at('src/engine/gpu/Readback.js');
const terrain=new TerrainData();new Village({scene:new Scene(),terrain,colliders:new Colliders()});
const field=computeShoreField(terrain,{res:512,swellDir:[WORLD.swellDir.x,WORLD.swellDir.y]});
const terrainGPU=new TerrainGPU(terrain,field),shore=new ShoreWaves(terrainGPU),sim=new ShoreSim(null,{terrainGPU,shore});
// In this pinned source TerrainGPU consumes the field upload without retaining
// a CPU copy. buildDirTexture therefore takes its documented terrain fallback.
// Preserve that behavior instead of attaching an invented CPU field reference.
if(shore.dirTexture||shore.uniforms.fields.dirOn.value!==0)throw Error('Source direction fallback changed');
const directionData=Buffer.from(new Uint8Array(new Float32Array([1,0,0,1]).buffer));
const originalKernel=sim.kernel.source;
if(mode){
 // Diagnostic only: retain the exact dependency functions and inspect the
 // phase/amplitude before transport. Never count this as a simulation result.
 sim.kernel.source=originalKernel.slice(0,originalKernel.lastIndexOf('@compute'))+`
@compute @workgroup_size(8,8,1)
fn main(@builtin(global_invocation_id) gid: vec3u) {
 if(gid.x>=768u || gid.y>=768u){return;}
 let uv=(vec2f(gid.xy)+0.5)/768.0;
 let p=shoreSimP.min+uv*shoreSimP.size;
 let ground=terrainHeightAt(p);
 if(frame.seaLevel-ground>7.0 || ground>3.2){textureStore(shoreSimOut,gid.xy,vec4f(0.0));return;}
 let ph=shorePhaseAt(p);
 let m=floor(ph.s+0.5);
 textureStore(shoreSimOut,gid.xy,vec4f(ground,ph.s,shoreHash1(m),shoreWaveAmp(m,ph.along)));
}`;
 if(mode==='--hash-probe')sim.kernel.source=originalKernel.slice(0,originalKernel.lastIndexOf('@compute'))+`
@compute @workgroup_size(8,8,1)
fn main(@builtin(global_invocation_id) gid: vec3u) {
 if(gid.x>=768u || gid.y>=768u){return;}
 let x=f32(gid.x+(gid.y/8u)*768u)-65536.0;
 let angle=x*127.1+311.7;
 let sine=sin(angle);
 let product=sine*43758.5453;
 var v=x;
 switch gid.y%8u {
  case 1u:{v=angle;}
  case 2u:{v=sine;}
  case 3u:{v=product;}
  case 4u:{v=shoreHash1(x);}
  case 5u:{v=fma(x,127.1,311.7);}
  case 6u:{v=sin(fma(x,127.1,311.7));}
  case 7u:{v=x*127.1;}
  default:{}
 }
 // Encode every f32 bit as four exactly representable half-float bytes.
 let bits=bitcast<u32>(v);
 textureStore(shoreSimOut,gid.xy,vec4f(f32(bits&255u),f32((bits>>8u)&255u),f32((bits>>16u)&255u),f32(bits>>24u)));
}`;
 sim.kernel.handle=GPU.computePipeline({label:'shore diagnostic',layout:GPU.device.createPipelineLayout({bindGroupLayouts:[sim.kernel.group0.layout,sim.kernel.bindings.layout]}),
 compute:{module:GPU.device.createShaderModule({code:sim.kernel.source}),entryPoint:'main'}});
}
const hash=b=>createHash('sha256').update(b).digest('hex');
const payloads={'kernel.wgsl':Buffer.from(sim.kernel.source),'heights.f32.bin':Buffer.from(new Uint8Array(terrain.heights.buffer)),
 'shore-field.rgba32.bin':Buffer.from(new Uint8Array(field.data.buffer)),
 'shore-direction.rgba32.bin':directionData,
 'sampling.wgsl':Buffer.from(sim.module.code),'deposit.wgsl':Buffer.from(sim.depositModule.code)};
for(const [name,block] of [['shore',shore.uniforms],['simulation',sim.uniforms],['terrain',terrainGPU.uniforms]]){
 block._pack();payloads[name+'-params.bin']=Buffer.from(new Uint8Array(block.data));payloads[name+'-params.wgsl']=Buffer.from(block.wgsl);
}
for(const [i,mip] of sim.lace.userData.mips.entries())payloads[`lace-m${i}.rgba8.bin`]=Buffer.from(mip.data);
const snapshots=[];
async function capture(frame,time){
 const prefix=`frame-${String(frame).padStart(4,'0')}`;
 FrameUniforms._pack();payloads[prefix+'-uniform.bin']=Buffer.from(new Uint8Array(FrameUniforms.data));
 payloads[prefix+'-state.rgba16.bin']=Buffer.from((await readTexture(sim.stateA)).data);
 payloads[prefix+'-deposit.u32.bin']=Buffer.from(await readBuffer(sim.deposit,768*768*4));
 snapshots.push({frame,time,prefix});
}
G.time.value=0;G.dt.value=0;G.seaLevel.value=0;FrameUniforms.fields.frameIndex.value=0;
GPU.submit();await GPU.queue.onSubmittedWorkDone();await capture(0,0);
let time=0;
for(let frame=1;frame<=1080;frame++){
 time+=1/60;GPU.beginFrame();G.time.value=time;G.dt.value=1/60;FrameUniforms.fields.frameIndex.value=frame;
 sim.update();GPU.submit();await GPU.queue.onSubmittedWorkDone();
 if([1,120,540,1080].includes(frame))await capture(frame,time);
}
const error=await GPU.device.popErrorScope();if(error)throw Error(error.message);
if(git('status','--porcelain'))throw Error('Source changed');
const bindings=sim.kernel.bindings.names.map((name,i)=>({name,binding:i,layout:sim.kernel.bindings.described[i].layout,decl:sim.kernel.bindings.described[i].decl}));
mkdirSync(output,{recursive:true});const files={};
for(const [name,data] of Object.entries(payloads)){writeFileSync(join(output,name),data,{flag:'wx'});files[name]=hash(data);}
writeFileSync(join(output,'shore-sim.json'),JSON.stringify({schema:'forge.tidewater.shore-sim',version:1,source_commit:pin,probe_kind:mode==='--hash-probe'?'hash_diagnostic':mode?'phase_diagnostic':'simulation',original_kernel_sha256:hash(Buffer.from(originalKernel)),
 resolution:768,domain:{min:[-180,-215],size:380},steps:1080,dt:1/60,shore_period:9,initial_state:'zero',direction_mode:'original_terrain_fallback',deposit_schedule:[],snapshots,bindings,
 terrain_sha256:files['heights.f32.bin'],files,exporter_sha256:hash(readFileSync(new URL(import.meta.url))),
 limitations:['no_native_execution_verdict','no_spray_deposition','two_fixed_shore_periods','constructor_defaults']},null,2),{flag:'wx'});
console.log(JSON.stringify({output,files:Object.keys(files).length,snapshots}));GPU.device.destroy();process.exit(0);
