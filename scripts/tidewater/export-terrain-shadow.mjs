// Independent source heightfield shadow kernel and numerical oracle.
import {readFileSync,writeFileSync,mkdirSync,existsSync} from 'node:fs';
import {resolve,join} from 'node:path';
import {pathToFileURL} from 'node:url';
import {execFileSync} from 'node:child_process';
import {createHash} from 'node:crypto';
const [sourceArg,environmentArg,outArg]=process.argv.slice(2);
if(!outArg) throw new Error('SOURCE ENVIRONMENT_EXPORT NEW_DIRECTORY required');
const source=resolve(sourceArg),out=resolve(outArg),environment=JSON.parse(readFileSync(join(resolve(environmentArg),'environment.json')));
const pin='1438b1abfcaee3267092b75573014f4d9b4a983c';
const git=(...a)=>execFileSync('git',['-C',source,...a],{encoding:'utf8'}).trim();
if(git('rev-parse','HEAD')!==pin||git('status','--porcelain')||environment.source_commit!==pin||existsSync(out)) throw new Error('Clean pin and new output required');
const moduleAt=p=>import(pathToFileURL(join(source,p)).href),hash=b=>createHash('sha256').update(b).digest('hex');
await moduleAt('test/headless.mjs');
const {GPU}=await moduleAt('src/engine/gpu/GPU.js');await GPU.init({headless:true});GPU.device.pushErrorScope('validation');
const {TerrainData}=await moduleAt('src/world/TerrainData.js');
const {TerrainGPU}=await moduleAt('src/world/TerrainGPU.js');
const {Village}=await moduleAt('src/world/Village.js');
const {Colliders}=await moduleAt('src/world/Colliders.js');
const {Scene}=await moduleAt('src/engine/index.js');
const {G}=await moduleAt('src/engine/render/Frame.js');
const {buildShadowHeights}=await moduleAt('src/world/terrain/TerrainBake.js');
const {Texture}=await moduleAt('src/engine/gpu/Texture.js');
const {readTexture}=await moduleAt('src/engine/gpu/Readback.js');
const terrain=new TerrainData();new Village({scene:new Scene(),terrain,colliders:new Colliders()});
const gpu=new TerrainGPU(terrain);
G.sunDir.value.set(...environment.sun_direction);gpu.updateSunShadow(null,true);
const original=gpu.sunShadowKernel.source;
const body=original.slice(original.lastIndexOf('@compute'));
const kernel=`struct ShadowParams{origin:f32,size:f32,sunBake:vec3f};
@group(0) @binding(0) var terrainShadowHeights:texture_2d<f32>;
@group(0) @binding(1) var smpLinearClamp:sampler;
@group(0) @binding(2) var terrainSunShadowOut:texture_storage_2d<rgba16float,write>;
@group(0) @binding(3) var<uniform> terrainParams:ShadowParams;\n`+body;
const lookupStart=gpu.module.code.indexOf('fn terrainSunShadowAt(');
const lookup=gpu.module.code.slice(lookupStart).replaceAll('terrainParams.sunBaked','1.0').replaceAll('terrainUvOf( P.xz )','((P.xz+1024.0)/2048.0)');
const packed=Buffer.concat(buildShadowHeights(terrain,4).levels.map(l=>Buffer.from(l.data.buffer,l.data.byteOffset,l.data.byteLength)));
const copied=new Texture({width:512,height:512,format:'rgba16float',usage:['storage','copySrc']});
const shader=GPU.device.createShaderModule({code:`
@group(0) @binding(0) var inputTex:texture_2d<f32>;
@group(0) @binding(1) var outputTex:texture_storage_2d<rgba16float,write>;
@compute @workgroup_size(8,8) fn main(@builtin(global_invocation_id) id:vec3u){
 textureStore(outputTex,vec2u(id.xy),textureLoad(inputTex,vec2i(id.xy),0));
}`});
const pipeline=GPU.device.createComputePipeline({layout:'auto',compute:{module:shader,entryPoint:'main'}});
const bg=GPU.device.createBindGroup({layout:pipeline.getBindGroupLayout(0),entries:[
{binding:0,resource:gpu.sunShadowTexture.getGPU().createView()},
{binding:1,resource:copied.getGPU().createView()}]});
const pass=GPU.getEncoder().beginComputePass();pass.setPipeline(pipeline);pass.setBindGroup(0,bg);pass.dispatchWorkgroups(64,64);pass.end();
const read=await readTexture(copied);
const error=await GPU.device.popErrorScope();if(error) throw new Error(error.message);
if(git('status','--porcelain')) throw new Error('Source changed');
mkdirSync(out,{recursive:true});const files={};
for(const [name,data] of Object.entries({'hill-shadow.wgsl':kernel,'hill-lookup.wgsl':lookup,'shadow-heights.r16.bin':packed,'shadow-reference.rgba16.bin':Buffer.from(read.data)})){
 const bytes=Buffer.isBuffer(data)?data:Buffer.from(data);writeFileSync(join(out,name),bytes,{flag:'wx'});files[name]=hash(bytes);
}
writeFileSync(join(out,'shadow.json'),JSON.stringify({schema:'forge.tidewater.hill-shadow-probe',version:1,source_commit:pin,
 heights_sha256:hash(Buffer.from(terrain.heights.buffer)),coarse_sha256:hash(packed),sun_direction:environment.sun_direction,files},null,2),{flag:'wx'});
console.log(JSON.stringify({out,files}));GPU.device.destroy();process.exit(0);
