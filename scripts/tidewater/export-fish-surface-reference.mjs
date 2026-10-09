// SPDX-License-Identifier: MIT
// Rasterize the complete acquired surface body, including fragment derivatives.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {pathToFileURL} from 'node:url';
import {execFileSync} from 'node:child_process';

const [sourceArg,packageArg,outputArg,libraryArg]=process.argv.slice(2);
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
 const bytes=fs.readFileSync(path.join(root,name));
 if(bytes.length!==pin.bytes||hash(bytes)!==pin.sha256)throw Error('Altered payload');
 return bytes;
};
// Check the entire portable package, not just the bindings sampled below.
for(const name of Object.keys(manifest.files))payload(name);
const props=JSON.parse(payload(manifest.props.file));
const material=JSON.parse(payload(manifest.material.file));
const surface=payload('material/main-surface.wgsl').toString('utf8');
const libraryBytes=libraryArg?fs.readFileSync(path.resolve(libraryArg)):null;
const library=libraryBytes?.toString('utf8');
if(library&&library.split(surface).length!==2)throw Error('Adapter must retain the complete original surface body exactly once');
const common=payload('material/module-0-common.wgsl').toString('utf8');
const extract=name=>{
 const start=common.indexOf('fn '+name+'(');if(start<0)throw Error('Missing helper '+name);
 let cursor=common.indexOf('{',start),depth=1;
 while(depth&&++cursor<common.length){if(common[cursor]==='{')depth++;if(common[cursor]==='}')depth--;}
 if(depth)throw Error('Unclosed helper');return common.slice(start,cursor+1);
};
const columns=40,tile=32,cases=props.instances.flatMap((instance,ordinal)=>Array.from({length:20},(_,part)=>({ordinal,source_id:instance.source_id,part,pattern:Math.floor(instance.record_f32[8])})));
const rows=Math.ceil(cases.length/columns),width=columns*tile,height=rows*tile;
const code=`
struct FishSkin { rows:array<vec4f,168> };
@group(0) @binding(0) var<storage,read> records:array<vec4f>;
@group(0) @binding(1) var<storage,read> fishSkin:FishSkin;
struct Frame { view:mat4x4f, time:f32 };
const frame=Frame(mat4x4f(vec4f(1,0,0,0),vec4f(0,1,0,0),vec4f(0,0,1,0),vec4f(0,0,0,1)),0.625);
${library??`${['luminance','interleavedGradientNoise','perturbNormalByHeight'].map(extract).join('\n')}
${payload('material/module-2-fish.wgsl').toString('utf8')}
struct Varyings {vFishLocal:vec3f,vFishData:vec4f,vFishInfo:vec4f,vFishFlags:vec4f};
struct FragmentInput {vs:Varyings,pixel:vec2f,P:vec3f,N:vec3f,V:vec3f};
struct Surface {albedo:vec3f,roughness:f32,metalness:f32,specularIntensity:f32,normal:vec3f,translucency:vec3f,clearcoat:f32,clearcoatRoughness:f32};`}
${library?'alias FragmentInput=FishSurfaceInput;':''}
struct VertexOutput {@builtin(position) position:vec4f};
@vertex fn vertex(@builtin(vertex_index) index:u32)->VertexOutput{
 let positions=array<vec2f,3>(vec2f(-1,-1),vec2f(3,-1),vec2f(-1,3));
 return VertexOutput(vec4f(positions[index],0,1));
}
struct Targets {@location(0) first:vec4f,@location(1) second:vec4f};
override plane:u32=0u;
@fragment fn fragment(v:VertexOutput)->Targets{
 let tileXY=vec2u(v.position.xy)/${tile}u;
 let caseIndex=tileXY.y*${columns}u+tileXY.x;
 if(caseIndex>=${cases.length}u){discard;}
 let ordinal=caseIndex/20u;let samplePart=caseIndex%20u;
 let uv=fract(v.position.xy/f32(${tile}));
 let record0=records[ordinal*4u];let record2=records[ordinal*4u+2u];let record3=records[ordinal*4u+3u];
 var in:FragmentInput;
 in.vs.vFishLocal=vec3f((uv.x-0.5)*0.16,(uv.y-0.5)*0.16,uv.x-0.5);
 in.vs.vFishData=vec4f(uv.x,f32(samplePart),uv.y,select(uv.y*2.0-1.0,uv.y*16.0,samplePart>0u&&samplePart<8u));
 in.vs.vFishInfo=vec4f(floor(record2.x),fract(record2.x),record0.w,0);
 in.vs.vFishFlags=record3;in.pixel=v.position.xy;
 in.P=vec3f((uv-0.5)*record0.w,0);in.N=vec3f(0,0,1);in.V=normalize(vec3f(0.3,0.2,1));
${library?'let s=fishSurface(in);':`var s:Surface;\n${surface}`}
 if(plane==0u){return Targets(vec4f(s.albedo,s.roughness),vec4f(s.normal,s.metalness));}
 return Targets(vec4f(s.translucency,s.specularIntensity),vec4f(s.clearcoat,s.clearcoatRoughness,0,1));
}
`;
await import(pathToFileURL(path.join(source,'test/headless.mjs')).href);
const {GPU}=await import(pathToFileURL(path.join(source,'src/engine/gpu/GPU.js')).href);
await GPU.init({headless:true});const device=GPU.device;
device.pushErrorScope('validation');
const shader=device.createShaderModule({code});
const info=await shader.getCompilationInfo();
if(info.messages.some(m=>m.type==='error'))throw Error(JSON.stringify(info.messages.map(m=>({type:m.type,message:m.message,line:m.lineNum,column:m.linePos}))));
const uploaded=bytes=>{
 const buffer=device.createBuffer({size:bytes.length,usage:GPUBufferUsage.STORAGE|GPUBufferUsage.COPY_DST});
 device.queue.writeBuffer(buffer,0,bytes);return buffer;
};
const recordBytes=payload('geometry/'+props.instance_stream.file),skinBytes=payload('material/'+material.skin_table.file);
const bindings=[uploaded(recordBytes),uploaded(skinBytes)];
const planes=[];
for(let plane=0;plane<2;plane++){
 const pipeline=await device.createRenderPipelineAsync({layout:'auto',vertex:{module:shader,entryPoint:'vertex'},fragment:{module:shader,entryPoint:'fragment',constants:{plane},targets:[{format:'rgba32float'},{format:'rgba32float'}]},primitive:{topology:'triangle-list'}});
 const textures=Array.from({length:2},()=>device.createTexture({size:[width,height],format:'rgba32float',usage:GPUTextureUsage.RENDER_ATTACHMENT|GPUTextureUsage.COPY_SRC}));
 const bytesPerRow=Math.ceil(width*16/256)*256;
 const buffers=textures.map(()=>device.createBuffer({size:bytesPerRow*height,usage:GPUBufferUsage.COPY_DST|GPUBufferUsage.MAP_READ}));
 const bind=device.createBindGroup({layout:pipeline.getBindGroupLayout(0),entries:bindings.map((buffer,binding)=>({binding,resource:{buffer}}))});
 const encoder=device.createCommandEncoder();
 const pass=encoder.beginRenderPass({colorAttachments:textures.map(texture=>({view:texture.createView(),loadOp:'clear',storeOp:'store',clearValue:[0,0,0,0]}))});
 pass.setPipeline(pipeline);pass.setBindGroup(0,bind);pass.draw(3);pass.end();
 textures.forEach((texture,index)=>encoder.copyTextureToBuffer({texture},{buffer:buffers[index],bytesPerRow},[width,height]));
 device.queue.submit([encoder.finish()]);
 for(const buffer of buffers){
  await buffer.mapAsync(GPUMapMode.READ);const mapped=new Uint8Array(buffer.getMappedRange());
  const bytes=Buffer.alloc(width*height*16);
  for(let row=0;row<height;row++)bytes.set(mapped.subarray(row*bytesPerRow,row*bytesPerRow+width*16),row*width*16);
  buffer.unmap();buffer.destroy();planes.push(bytes);
 }
 textures.forEach(texture=>texture.destroy());
}
const error=await device.popErrorScope();if(error)throw Error(error.message);
const floats=planes.map(bytes=>new Float32Array(bytes.buffer,bytes.byteOffset,bytes.length/4));
if(floats.some(values=>!values.every(Number.isFinite)))throw Error('Non-finite raster surface');
for(const [index,row] of cases.entries()){
 let covered=0;const x0=index%columns*tile,y0=Math.floor(index/columns)*tile;
 for(let y=y0;y<y0+tile;y++)for(let x=x0;x<x0+tile;x++)if(floats[3][(y*width+x)*4+3]===1)covered++;
 if(!covered)throw Error('Empty material tile '+index);row.covered_pixels=covered;
}
if(git('status','--porcelain','--untracked-files=no'))throw Error('Source changed');
fs.mkdirSync(output);const files={};
const names=['albedo-roughness-f32.bin','normal-metalness-f32.bin','translucency-specular-f32.bin','clearcoat-coverage-f32.bin'];
for(const [name,bytes] of [...planes.map((bytes,index)=>[names[index],bytes]),['surface.wgsl',Buffer.from(code)],['records-f32.bin',recordBytes],['skin-f32.bin',skinBytes]]){
 fs.writeFileSync(path.join(output,name),bytes,{flag:'wx'});files[name]={bytes:bytes.length,sha256:hash(bytes)};
}
const descriptor={schema:'forge.tidewater.fish-surface-reference',version:1,source_commit:commit,package_sha256:hash(manifestBytes),source_surface_sha256:hash(Buffer.from(surface)),specular_mode:material.specular_mode,gpu_executed:true,complete_source_surface_body:true,width,height,tile,columns,time:0.625,cases,files,limits:['Diagnostic planar domains for every source instance and part, not rasterized map geometry.','Surface outputs only; shared scene lighting, shadows, LOD and browser visual parity remain unverified.','Intended specular mode retained; historical Three.js reference mode still requires comparison.']};
if(libraryBytes){
 descriptor.adapter_library_sha256=hash(libraryBytes);
 descriptor.specular_mode=library.slice(library.indexOf(surface)+surface.length).includes('s.specularIntensity = 0.0;')?'reference':material.specular_mode;
 descriptor.limits[2]='Explicit adapter specular mode recorded; rendered Three.js scene parity remains unverified.';
}
fs.writeFileSync(path.join(output,'reference.json'),JSON.stringify(descriptor,null,2),{flag:'wx'});
console.log(JSON.stringify({cases:cases.length,width,height,gpu_executed:true,complete_source_surface_body:true,source_unchanged:true}));
device.destroy();process.exit(0);
