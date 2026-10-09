// SPDX-License-Identifier: MIT
// Compile the Rust-assembled material's complete lighting closure on WebGPU.
// Bevy's imported camera/mesh functions are represented by explicit probe stubs;
// this check does not draw the material or claim integration in Studio.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {pathToFileURL} from 'node:url';
import {execFileSync} from 'node:child_process';

const [sourceArg,nativeArg,outputArg]=process.argv.slice(2);
if(!sourceArg||!nativeArg||!outputArg||process.versions.node!=='22.22.3')throw Error('Pinned Node, SOURCE NATIVE_PROOF NEW_OUTPUT required');
const source=path.resolve(sourceArg),native=path.resolve(nativeArg),output=path.resolve(outputArg);
const sha=bytes=>crypto.createHash('sha256').update(bytes).digest('hex');
if(fs.existsSync(output))throw Error('Fresh output required');
const git=(...args)=>execFileSync('git',['-C',source,...args],{encoding:'utf8'}).trim();
if(git('rev-parse','HEAD')!=='1438b1abfcaee3267092b75573014f4d9b4a983c'||git('status','--porcelain','--untracked-files=no'))throw Error('Intact source required');
const nativeProof=JSON.parse(fs.readFileSync(path.join(native,'result.json')));
if(!nativeProof.inputs_unchanged||Object.values(nativeProof.steps).some(code=>code!==0))throw Error('Passing native proof required');
for(const [name,pin] of Object.entries(nativeProof.inputs))if(sha(fs.readFileSync(name))!==pin)throw Error('Native source changed: '+name);
const shaderBytes=fs.readFileSync(path.join(native,'fish.wgsl')),shader=shaderBytes.toString('utf8');
const importLine='#import bevy_pbr::{mesh_functions, mesh_view_bindings::view,\n    view_transformations::position_world_to_clip}';
if(shader.split(importLine).length!==2)throw Error('Bevy import seam drift');
const stub=`
struct ProbeView {view_from_world:mat4x4f,world_position:vec3f};
const identity=mat4x4f(vec4f(1,0,0,0),vec4f(0,1,0,0),vec4f(0,0,1,0),vec4f(0,0,0,1));
const view=ProbeView(identity,vec3f(0,0,2));
fn probeWorld(instance:u32)->mat4x4f{return identity;}
fn probeNormal(normal:vec3f,instance:u32)->vec3f{return normal;}
fn position_world_to_clip(P:vec3f)->vec4f{return vec4f(P,1);}
`;
await import(pathToFileURL(path.join(source,'test/headless.mjs')).href);
const {GPU}=await import(pathToFileURL(path.join(source,'src/engine/gpu/GPU.js')).href);
await GPU.init({headless:true});const device=GPU.device,variants=[];
const outputs=[];
for(const shared of [false,true]){
 let code=shader.replace(importLine,stub).replaceAll('mesh_functions::get_world_from_local','probeWorld').replaceAll('mesh_functions::mesh_normal_local_to_world','probeNormal').replaceAll('#{MATERIAL_BIND_GROUP}','0');
 const branch=/#ifdef FORGE_SHARED_SCENE_LIGHTING\n([^]*?)#else\n([^]*?)#endif/g;
 if([...code.matchAll(branch)].length!==1)throw Error('Expected one shared-scene branch');
 code=code.replace(branch,(_,live,uniform)=>shared?live:uniform);
 if(/^#/m.test(code))throw Error('Unresolved preprocessor directive');
 device.pushErrorScope('validation');
 const module=device.createShaderModule({code});
 const info=await module.getCompilationInfo();
 const messages=info.messages.map(item=>({type:item.type,message:item.message,line:item.lineNum,column:item.linePos}));
 if(messages.some(item=>item.type==='error'))throw Error(JSON.stringify(messages));
 await device.createRenderPipelineAsync({layout:'auto',vertex:{module,entryPoint:'vertex',buffers:[{arrayStride:52,attributes:[{shaderLocation:0,offset:0,format:'float32x3'},{shaderLocation:1,offset:12,format:'float32x3'},{shaderLocation:2,offset:24,format:'float32x3'},{shaderLocation:3,offset:36,format:'float32x4'}]}]},fragment:{module,entryPoint:'fragment',targets:[{format:'rgba16float'}]},primitive:{topology:'triangle-list',cullMode:'back'}});
 const error=await device.popErrorScope();if(error)throw Error(error.message);
 const name=shared?'shared-scene.wgsl':'uniform-scene.wgsl';
 outputs.push([name,Buffer.from(code)]);variants.push({shared_scene_lighting:shared,pipeline_created:true,messages});
}
if(git('status','--porcelain','--untracked-files=no'))throw Error('Reference changed');
fs.mkdirSync(output);const files={};
for(const [name,bytes] of outputs){fs.writeFileSync(path.join(output,name),bytes,{flag:'wx'});files[name]={bytes:bytes.length,sha256:sha(bytes)};}
const receipt={status:'passed',shader_sha256:sha(shaderBytes),native_proof_sha256:sha(fs.readFileSync(path.join(native,'result.json'))),variants,files,source_unchanged:true,gpu_pipeline_created:true,gpu_draw_executed:false,bevy_imports_stubbed:true,studio_executed:false,limits:['Compilation and pipeline construction only','Explicit probe camera/mesh imports; actual Bevy draw and bind groups remain unverified','No visual parity claim']};
fs.writeFileSync(path.join(output,'result.json'),JSON.stringify(receipt,null,2),{flag:'wx'});
console.log(JSON.stringify(receipt));device.destroy();process.exit(0);
