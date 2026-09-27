// Development acquisition of original atmosphere/cloud/environment programs.
// Fixed replay from cold state; not a claim to reproduce App precompile history.
import {readFileSync,writeFileSync,mkdirSync,existsSync} from 'node:fs';
import {resolve,join} from 'node:path';
import {pathToFileURL} from 'node:url';
import {execFileSync} from 'node:child_process';
import {createHash} from 'node:crypto';
import {CloudCapture} from './lib/cloud-capture.mjs';
const [sourceArg,referenceArg,outputArg,option]=process.argv.slice(2);
if(option && option!=='--clouds') throw new Error('Unknown acquisition option');
if(process.argv.length>6) throw new Error('Too many arguments');
if(!sourceArg||!referenceArg||!outputArg) throw new Error('SOURCE REFERENCE_JSON NEW_DIRECTORY required');
const source=resolve(sourceArg),output=resolve(outputArg);
const pin='1438b1abfcaee3267092b75573014f4d9b4a983c';
const git=(...args)=>execFileSync('git',['-C',source,...args],{encoding:'utf8'}).trim();
if(git('rev-parse','HEAD')!==pin||git('status','--porcelain')) throw new Error('Clean pinned source required');
if(existsSync(output)||output.startsWith(source+'/')||output.startsWith(source+'\\')) throw new Error('New directory outside source required');
const referenceBytes=readFileSync(resolve(referenceArg)),reference=JSON.parse(referenceBytes);
if(reference.source_commit!==pin||reference.status!=='ready_reference_only') throw new Error('Pinned reference required');
const moduleAt=p=>import(pathToFileURL(join(source,p)).href);
await moduleAt('test/headless.mjs');
const {GPU}=await moduleAt('src/engine/gpu/GPU.js');
await GPU.init({headless:true});GPU.device.pushErrorScope('validation');
const {Atmosphere,SUN_ILLUMINANCE}=await moduleAt('src/sky/Atmosphere.js');
const {Sky,sunDirectionFromTime}=await moduleAt('src/sky/Sky.js');
const {SkyProClouds}=await moduleAt('src/sky/SkyProClouds.js');
const {Environment}=await moduleAt('src/sky/Environment.js');
const {G,FrameUniforms,setFrameCamera}=await moduleAt('src/engine/render/Frame.js');
const {PerspectiveCamera,Scene,Vector3,MathUtils,Color}=await moduleAt('src/engine/index.js');
const {readBuffer,readTexture}=await moduleAt('src/engine/gpu/Readback.js');
const {Texture}=await moduleAt('src/engine/gpu/Texture.js');
const assets={};
// Only the two original cloud noise files are served to the original loader.
const originalFetch=globalThis.fetch;
globalThis.fetch=async url=>{
  if(!['/clouds/baseShape64.bin','/clouds/blueNoise.bin'].includes(url)) throw new Error(`Unexpected acquisition fetch: ${url}`);
  const bytes=readFileSync(join(source,'public',url.slice(1)));
  assets[url]=createHash('sha256').update(bytes).digest('hex');
  return new Response(bytes);
};
const atmosphere=new Atmosphere(),sky=new Sky(atmosphere),clouds=new SkyProClouds(null,atmosphere);
try{await clouds.ready;}finally{globalThis.fetch=originalFetch;}
sky.clouds=clouds;
if(reference.cloud_coverage!==undefined){
 if(!Number.isFinite(reference.cloud_coverage)||reference.cloud_coverage<0||reference.cloud_coverage>1)throw Error('Invalid cloud coverage');
 clouds.coverage.value=reference.cloud_coverage;
}

const cloudCapture=option==='--clouds'?new CloudCapture(clouds,FrameUniforms,source):null;
const environment=new Environment(null,new Scene(),sky);
const [width,height]=reference.output,c=reference.camera;
const camera=new PerspectiveCamera(c.fov,width/height,.06,60000);
camera.position.set(c.position.x,c.position.y,c.position.z);
camera.quaternion.set(c.quaternion._x,c.quaternion._y,c.quaternion._z,c.quaternion._w);
camera.updateMatrixWorld();clouds.outputSize={x:width,y:height};
const direction=sunDirectionFromTime(reference.settings.timeOfDay).applyAxisAngle(new Vector3(0,1,0),MathUtils.degToRad(reference.settings.sunAzimuth||0));
atmosphere.sunDir.value.copy(direction);
const night=MathUtils.smoothstep(-direction.y,.02,.18);
G.night.value=night;sky.starIntensity.value=night;
const moon=new Vector3(-direction.x,Math.abs(direction.y)*.8+.25,-direction.z).normalize();
sky.moonDir.value.copy(moon);G.sunDir.value.copy(direction.y>-.07?direction:moon);
const dt=1/60;
if(reference.global_wind){
 const {speed,direction}=reference.global_wind;
 if(!Number.isFinite(speed)||speed<0||!Array.isArray(direction)||direction.length!==2||!direction.every(Number.isFinite)||Math.hypot(...direction)<1e-6)throw Error('Invalid reference global wind');
 G.windSpeed.value=speed;G.windDir.value.set(...direction);
}
for(let i=0;i<120;i++){
  GPU.beginFrame();
  G.time.value=(i+1)*dt;G.dt.value=dt;FrameUniforms.fields.frameIndex.value=i;
  setFrameCamera(camera,width,height);
  atmosphere.update(dt,camera.position.y);
  // Explicit readback removes scheduling ambiguity in the cold-state oracle.
  if(i===0) atmosphere._onIrradiance(await readBuffer(atmosphere.irrBuffer,48));
  const fade=MathUtils.smoothstep(direction.y,-.03,.02),T=atmosphere.sunTransmittance;
  const sun=direction.y>-.07?new Color(...T).multiplyScalar(SUN_ILLUMINANCE*fade):new Color(.6,.7,1).multiplyScalar(.12*night);
  G.sunColor.value.copy(sun);
  const irr=atmosphere.skyIrradiance,ambient=.012*night;
  G.skyIrradiance.value.setRGB(irr[0]+ambient*.6,irr[1]+ambient*.7,irr[2]+ambient);
  G.horizonColor.value.setRGB(...atmosphere.horizon);
  clouds.update(dt,camera);environment.update(dt);
  cloudCapture?.frame(i+1);
  GPU.submit();await GPU.queue.onSubmittedWorkDone();
  if(cloudCapture && [1,120].includes(i+1)) await cloudCapture.snapshot(i+1,readTexture);
}
const files={};mkdirSync(output,{recursive:true});
const save=(name,data)=>{const bytes=Buffer.from(data);writeFileSync(join(output,name),bytes,{flag:'wx'});files[name]=createHash('sha256').update(bytes).digest('hex');return name;};
if(cloudCapture) cloudCapture.save(save);
const sh=save('irradiance-sh.f32.bin',await readBuffer(environment.sh[environment.front],144));
// Original cube lacks COPY_SRC; identity load/store preserves all half floats.
const shader=GPU.device.createShaderModule({code:`
@group(0) @binding(0) var source:texture_2d_array<f32>;
@group(0) @binding(1) var copied:texture_storage_2d_array<rgba16float,write>;
@compute @workgroup_size(8,8) fn main(@builtin(global_invocation_id) id:vec3u){
 if(any(id.xy>=textureDimensions(copied))||id.z>=6u){return;}
 textureStore(copied,vec2i(id.xy),i32(id.z),textureLoad(source,vec2i(id.xy),i32(id.z),0));
}`});
const pipeline=GPU.device.createComputePipeline({layout:'auto',compute:{module:shader,entryPoint:'main'}});
const cube=environment.texture;
const staging=new Texture({width:cube.width,height:cube.height,dimension:'cube',format:'rgba16float',mips:cube.mipLevelCount,usage:['sample','storage','copySrc']});
const mips=[];
for(let level=0;level<cube.mipLevelCount;level++){
 const view={dimension:'2d-array',baseMipLevel:level,mipLevelCount:1};
 const group=GPU.device.createBindGroup({layout:pipeline.getBindGroupLayout(0),entries:[
 {binding:0,resource:cube.getGPU().createView(view)},
 {binding:1,resource:staging.getGPU().createView(view)}]});
 const size=cube.width>>level,pass=GPU.getEncoder().beginComputePass();
 pass.setPipeline(pipeline);pass.setBindGroup(0,group);pass.dispatchWorkgroups(Math.ceil(size/8),Math.ceil(size/8),6);pass.end();
 const faces=[];
 for(let face=0;face<6;face++){
  const read=await readTexture(staging,{mip:level,layer:face});
  faces.push(save(`specular-${level}-${face}.rgba16.bin`,read.data));
 }
 mips.push({level,width:size,height:size,faces});
}
const error=await GPU.device.popErrorScope();if(error) throw new Error(error.message);
if(git('status','--porcelain')) throw new Error('Source changed');
const vec=v=>[v.x,v.y,v.z],color=v=>[v.r,v.g,v.b];
writeFileSync(join(output,'environment.json'),JSON.stringify({schema:'forge.tidewater.environment-probe',version:1,
 source_commit:pin,reference_sha256:createHash('sha256').update(referenceBytes).digest('hex'),
 acquisition:'original_atmosphere_skyproclouds_environment_cold_120_frames',time:2,step:dt,camera:c,
 sun_direction:vec(G.sunDir.value),sun_color:color(G.sunColor.value),sky_irradiance:color(G.skyIrradiance.value),
 environment_intensity:FrameUniforms.fields.envIntensity.value,night,sh,mips,assets,files,
 cloud_coverage:clouds.coverage.value,
 wind:{speed:G.windSpeed.value,direction:[G.windDir.value.x,G.windDir.value.y]},
 limitations:['cold_state_not_app_precompile_replay','fixed_camera_and_time','no_native_sky_or_cloud_implementation','no_cross_adapter_bit_identity']},null,2),{flag:'wx'});
console.log(JSON.stringify({output,files:Object.keys(files).length,sun:color(G.sunColor.value),status:'acquired_cold_environment'}));
GPU.device.destroy();process.exit(0);
