// SPDX-License-Identifier: MIT
// Execute the acquired source vertex body as a compute kernel for port validation.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {pathToFileURL} from 'node:url';
import {execFileSync} from 'node:child_process';

const [sourceArg,packageArg,outputArg]=process.argv.slice(2);
if(!sourceArg||!packageArg||!outputArg||process.versions.node!=='22.22.3')throw Error('Pinned Node, SOURCE PACKAGE NEW_OUTPUT required');
const source=path.resolve(sourceArg),root=path.resolve(packageArg),output=path.resolve(outputArg);
const hash=bytes=>crypto.createHash('sha256').update(bytes).digest('hex');
const commit='1438b1abfcaee3267092b75573014f4d9b4a983c';
const git=(...args)=>execFileSync('git',['-C',source,...args],{encoding:'utf8'}).trim();
if(git('rev-parse','HEAD')!==commit||git('status','--porcelain','--untracked-files=no'))throw Error('Intact reference required');
if(fs.existsSync(output))throw Error('Fresh output required');
for(const parent of [source,root]){
  const relative=path.relative(parent,output);
  if(!relative||(!relative.startsWith('..')&&!path.isAbsolute(relative)))throw Error('External output required');
}
const manifestBytes=fs.readFileSync(path.join(root,'package.json'));
if(hash(manifestBytes)!=='e578b44e93d18d79ec06c875b76dae005f71c17ecf1d0ff09aa3ecddd651330a')throw Error('Package pin differs');
const manifest=JSON.parse(manifestBytes);
const payload=name=>{
  const pin=manifest.files[name];if(!pin)throw Error('Unknown payload');
  const data=fs.readFileSync(path.join(root,name));
  if(data.length!==pin.bytes||hash(data)!==pin.sha256)throw Error('Altered payload');
  return data;
};
const props=JSON.parse(payload(manifest.props.file));
const material=JSON.parse(payload(manifest.material.file));
const localReference=ref=>payload('geometry/'+ref.file);
const floatValues=bytes=>new Float32Array(bytes.buffer.slice(bytes.byteOffset,bytes.byteOffset+bytes.byteLength));
const cases=[];let count=0;
for(const [ordinal,instance] of props.instances.entries()){
  const prototype=props.prototypes.find(p=>p.key===instance.prototype);
  if(!prototype)throw Error('Prototype absent');
  for(const lod of prototype.lods){
    cases.push({ordinal,source_id:instance.source_id,prototype:prototype.key,lod:lod.lod,start:count,vertices:lod.vertices,geometry:lod});
    count+=lod.vertices;
  }
}
const input=new Float32Array(count*16);
for(const row of cases){
  const g=row.geometry,position=floatValues(localReference(g.attributes.position)),normal=floatValues(localReference(g.attributes.normal)),data=floatValues(localReference(g.attributes.aData));
  for(let vertex=0;vertex<row.vertices;vertex++){
    const start=(row.start+vertex)*16;
    input.set(position.subarray(vertex*3,vertex*3+3),start);
    input.set(normal.subarray(vertex*3,vertex*3+3),start+4);
    input.set(data.subarray(vertex*4,vertex*4+4),start+8);
    input[start+12]=row.ordinal;input[start+13]=props.instances[row.ordinal].kind0;
  }
}
const module=material.modules.find(m=>m.name==='fish');if(!module)throw Error('Fish module absent');
const originalVertex=payload('material/main-vertex.wgsl').toString('utf8');
const code=`
struct Input { position:vec4f, normal:vec4f, data:vec4f, ids:vec4f };
struct PoseOutput { position:vec4f, normal:vec4f };
struct FishSkin { rows:array<vec4f,168> };
@group(0) @binding(0) var<storage,read> inputs:array<Input>;
@group(0) @binding(1) var<storage,read> records:array<vec4f>;
@group(0) @binding(2) var<storage,read> fishSkin:FishSkin;
@group(0) @binding(3) var<storage,read_write> outputs:array<PoseOutput>;
fn fishPropsRecordIndex(kind:u32,instance:u32)->u32{return instance;}
fn fishPropsRecord(instance:u32,row:u32)->vec4f{return records[instance*4u+row];}
${payload('material/'+module.code).toString('utf8')}
struct SourceVertex { position:vec3f,normal:vec3f,aData:vec4f,aKind:f32,instance:u32,useWorld:bool,worldNormal:vec3f,worldPos:vec3f,prevWorldPos:vec3f };
struct SourceOutput {vFishFade:vec2f,vFishLocal:vec3f,vFishData:vec4f,vFishInfo:vec4f,vFishFlags:vec4f};
fn sourcePose(x:Input)->PoseOutput{
 var v:SourceVertex;v.position=x.position.xyz;v.normal=x.normal.xyz;v.aData=x.data;v.instance=u32(x.ids.x);v.aKind=x.ids.y;
 var o:SourceOutput;
${originalVertex}
 return PoseOutput(vec4f(v.worldPos,1.0),vec4f(v.worldNormal,0.0));
}
@compute @workgroup_size(64)
fn main(@builtin(global_invocation_id) id:vec3u){if(id.x<arrayLength(&inputs)){outputs[id.x]=sourcePose(inputs[id.x]);}}
`;
await import(pathToFileURL(path.join(source,'test/headless.mjs')).href);
const {GPU}=await import(pathToFileURL(path.join(source,'src/engine/gpu/GPU.js')).href);
await GPU.init({headless:true});const device=GPU.device;
device.pushErrorScope('validation');
const shader=device.createShaderModule({code});
const info=await shader.getCompilationInfo();
if(info.messages.some(m=>m.type==='error'))throw Error(JSON.stringify(info.messages));
const pipeline=await device.createComputePipelineAsync({layout:'auto',compute:{module:shader,entryPoint:'main'}});
const uploaded=bytes=>{
  if(bytes.byteLength>device.limits.maxStorageBufferBindingSize)throw Error('Binding too large');
  const b=device.createBuffer({size:bytes.byteLength,usage:GPUBufferUsage.STORAGE|GPUBufferUsage.COPY_DST});
  device.queue.writeBuffer(b,0,bytes);return b;
};
const a=uploaded(input),b=uploaded(localReference(props.instance_stream)),c=uploaded(payload('material/'+material.skin_table.file));
const byteLength=count*32;
const outputBuffer=device.createBuffer({size:byteLength,usage:GPUBufferUsage.STORAGE|GPUBufferUsage.COPY_SRC});
const readback=device.createBuffer({size:byteLength,usage:GPUBufferUsage.COPY_DST|GPUBufferUsage.MAP_READ});
const bind=device.createBindGroup({layout:pipeline.getBindGroupLayout(0),entries:[a,b,c,outputBuffer].map((buffer,binding)=>({binding,resource:{buffer}}))});
const encoder=device.createCommandEncoder();const pass=encoder.beginComputePass();pass.setPipeline(pipeline);pass.setBindGroup(0,bind);pass.dispatchWorkgroups(Math.ceil(count/64));pass.end();
encoder.copyBufferToBuffer(outputBuffer,0,readback,0,byteLength);device.queue.submit([encoder.finish()]);
await readback.mapAsync(GPUMapMode.READ);
const result=Buffer.from(new Uint8Array(readback.getMappedRange()).slice());readback.unmap();
const error=await device.popErrorScope();if(error)throw Error(error.message);
if(!floatValues(result).every(Number.isFinite))throw Error('Non-finite source pose');
if(git('status','--porcelain','--untracked-files=no'))throw Error('Source changed');
fs.mkdirSync(output);const files={};
for(const [name,bytes] of Object.entries({'input-f32.bin':Buffer.from(input.buffer),'output-f32.bin':result,'records-f32.bin':localReference(props.instance_stream),'skin-f32.bin':payload('material/'+material.skin_table.file),'pose.wgsl':Buffer.from(code)})){
 fs.writeFileSync(path.join(output,name),bytes,{flag:'wx'});files[name]={bytes:bytes.length,sha256:hash(bytes)};
}
const descriptor={schema:'forge.tidewater.fish-pose-reference',version:1,source_commit:commit,package_sha256:hash(manifestBytes),source_vertex_sha256:hash(Buffer.from(originalVertex)),gpu_executed:true,complete_source_vertex_body:true,vertices:count,cases:cases.map(({geometry,...rest})=>rest),files,limits:['Pose compute reference only; full material shading, LOD culling and rendered visual parity unverified']};
fs.writeFileSync(path.join(output,'reference.json'),JSON.stringify(descriptor,null,2),{flag:'wx'});
console.log(JSON.stringify({vertices:count,cases:cases.length,gpu_executed:true,source_unchanged:true}));
device.destroy();process.exit(0);
