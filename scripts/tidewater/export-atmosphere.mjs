// Pinned atmosphere kernels and numerical oracles, independent of cloud history.
import {readFileSync,writeFileSync,mkdirSync,existsSync} from 'node:fs';
import {resolve,join} from 'node:path';
import {pathToFileURL} from 'node:url';
import {execFileSync} from 'node:child_process';
import {createHash} from 'node:crypto';
const [sourceArg,environmentArg,outputArg]=process.argv.slice(2);
if(!outputArg)throw new Error('SOURCE ENVIRONMENT_EXPORT NEW_DIRECTORY required');
const source=resolve(sourceArg),output=resolve(outputArg),pin='1438b1abfcaee3267092b75573014f4d9b4a983c';
const git=(...a)=>execFileSync('git',['-C',source,...a],{encoding:'utf8'}).trim();
if(git('rev-parse','HEAD')!==pin||git('status','--porcelain'))throw new Error('Clean pinned source required');
if(existsSync(output)||output.startsWith(source+'/')||output.startsWith(source+'\\'))throw new Error('New output outside source required');
const env=JSON.parse(readFileSync(join(environmentArg,'environment.json')));
if(env.source_commit!==pin||env.night!==0)throw new Error('Daytime environment required: its key light must be the real sun');
const moduleAt=p=>import(pathToFileURL(join(source,p)).href);
await moduleAt('test/headless.mjs');
const {GPU}=await moduleAt('src/engine/gpu/GPU.js');
const {Atmosphere}=await moduleAt('src/sky/Atmosphere.js');
const {Texture}=await moduleAt('src/engine/gpu/Texture.js');
const {readTexture,readBuffer}=await moduleAt('src/engine/gpu/Readback.js');
await moduleAt('src/engine/render/Frame.js');
await GPU.init({headless:true});GPU.device.pushErrorScope('validation');
const atmosphere=new Atmosphere();
atmosphere.sunDir.value.set(...env.sun_direction);
GPU.beginFrame();atmosphere.update(1/60,env.camera.position.y);
GPU.submit();await GPU.queue.onSubmittedWorkDone();
const payloads={};
atmosphere.params._pack();payloads['params.bin']=Buffer.from(atmosphere.params.data);
payloads['irradiance.bin']=Buffer.from(await readBuffer(atmosphere.irrBuffer,64));
const dimensions={transmittance:[256,64],multiScat:[32,32],skyView:[192,108]};
const shader=GPU.device.createShaderModule({code:`
@group(0) @binding(0) var source:texture_2d<f32>;
@group(0) @binding(1) var targetMap:texture_storage_2d<rgba16float,write>;
@compute @workgroup_size(8,8) fn main(@builtin(global_invocation_id) id:vec3u){
 if(any(id.xy>=textureDimensions(targetMap))){return;}
 textureStore(targetMap,id.xy,textureLoad(source,id.xy,0));
}`});
const pipeline=GPU.device.createComputePipeline({layout:'auto',compute:{module:shader,entryPoint:'main'}});
for(const [name,[width,height]] of Object.entries(dimensions)){
 payloads[name+'.wgsl']=Buffer.from(atmosphere[name+'Kernel'].source);
 const copied=new Texture({width,height,format:'rgba16float',usage:['storage','copySrc']});
 const group=GPU.device.createBindGroup({layout:pipeline.getBindGroupLayout(0),entries:[
  {binding:0,resource:atmosphere[name+'LUT'].getGPU().createView()},
  {binding:1,resource:copied.getGPU().createView()}]});
 const pass=GPU.getEncoder().beginComputePass();pass.setPipeline(pipeline);pass.setBindGroup(0,group);
 pass.dispatchWorkgroups(Math.ceil(width/8),Math.ceil(height/8));pass.end();
 payloads[name+'.rgba16.bin']=Buffer.from((await readTexture(copied)).data);
}
payloads['irradiance.wgsl']=Buffer.from(atmosphere.irradianceKernel.source);
payloads['lookup.wgsl']=Buffer.from(atmosphere.module.code);
const {Sky}=await moduleAt('src/sky/Sky.js');
const {commonModule}=await moduleAt('src/engine/render/wgsl/common.js');
payloads['sky-module.wgsl']=Buffer.from(new Sky(atmosphere).module.code);
payloads['atmosphere-core.wgsl']=Buffer.from(atmosphere.coreModule.code);
payloads['atmosphere-transmittance.wgsl']=Buffer.from(atmosphere.transmittanceModule.code);
payloads['source-common.wgsl']=Buffer.from(commonModule.code);
// Separate diagnostic: preserve the original LUTs while exposing the sphere
// intersection and the path length prescribed by the transmittance coordinates.
const {ComputeKernel}=await moduleAt('src/engine/gpu/Compute.js');
const boundary=new Texture({width:256,height:64,format:'rgba16float',usage:['storage','copySrc']});
const original=atmosphere.transmittanceKernel.source;
const store='textureStore( outLUT, px, vec4f( exp( - od ), 1.0 ) );';
if(!original.includes(store))throw new Error('Transmittance diagnostic anchor changed');
const boundaryKernel=new ComputeKernel({label:'Atmosphere Boundary Diagnostic',modules:[atmosphere.coreModule],
 bindings:{outLUT:{storageTexture:boundary}},workgroupSize:[8,8,1],
 code:original.slice(original.indexOf('@compute')).replace(store,
  'textureStore( outLUT, px, vec4f( tMax, d, dot(ro,ro)-ATMO_RT*ATMO_RT, atmosphereParams.viewHeight ) );')});
boundaryKernel.dispatch([32,8,1]);
payloads['boundary.wgsl']=Buffer.from(boundaryKernel.source);
payloads['boundary.rgba16.bin']=Buffer.from((await readTexture(boundary)).data);
const error=await GPU.device.popErrorScope();if(error)throw new Error(error.message);
if(git('status','--porcelain'))throw new Error('Source changed');
mkdirSync(output,{recursive:true});const files={};
for(const [name,data] of Object.entries(payloads)){
 writeFileSync(join(output,name),data,{flag:'wx'});files[name]=createHash('sha256').update(data).digest('hex');
}
writeFileSync(join(output,'atmosphere.json'),JSON.stringify({schema:'forge.tidewater.atmosphere-probe',version:1,
 source_commit:pin,camera_y:env.camera.position.y,real_sun_direction:env.sun_direction,
 view_height:atmosphere.viewHeight.value,dimensions,files,
 limitations:['fixed_daytime_parameters','no_clouds_or_sky_composition','no_native_implementation_verdict']},null,2),{flag:'wx'});
console.log(JSON.stringify({output,files:Object.keys(files).length,view_height:atmosphere.viewHeight.value}));
GPU.device.destroy();process.exit(0);
