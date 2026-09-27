// Diagnostic copy only: original shadow attachment lacks COPY_SRC usage.
import {writeFileSync,existsSync} from 'node:fs';
import {createHash} from 'node:crypto';
export async function captureObjectShadows(moduleAt, shadows, output, scene) {
 const {GPU}=await moduleAt('src/engine/gpu/GPU.js');
 const {Texture}=await moduleAt('src/engine/gpu/Texture.js');
 const {readTexture}=await moduleAt('src/engine/gpu/Readback.js');
 const {ShadowUniforms,shadowModule}=await moduleAt('src/engine/render/wgsl/lighting.js');
 GPU.device.pushErrorScope('validation');
 const names=Array.from({length:shadows.count},(_,i)=>output+`.cascade-${i}.r32.bin`);
 if(names.some(existsSync)||existsSync(output+'.shadows.json')||existsSync(output+'.shadow-filter.wgsl')) throw new Error('Shadow outputs exist');
 const copied=new Texture({width:shadows.size,height:shadows.size,depth:shadows.count,dimension:'2d-array',format:'r32float',usage:['storage','copySrc']});
 const shader=GPU.device.createShaderModule({code:`
 @group(0) @binding(0) var source:texture_depth_2d_array;
 @group(0) @binding(1) var outputMap:texture_storage_2d_array<r32float,write>;
 @compute @workgroup_size(8,8) fn main(@builtin(global_invocation_id) id:vec3u){
  textureStore(outputMap,vec2i(id.xy),i32(id.z),vec4f(textureLoad(source,vec2i(id.xy),i32(id.z),0)));
 }`});
 const pipeline=GPU.device.createComputePipeline({layout:'auto',compute:{module:shader,entryPoint:'main'}});
 const group=GPU.device.createBindGroup({layout:pipeline.getBindGroupLayout(0),entries:[
  {binding:0,resource:shadows.texture.getGPU().createView({dimension:'2d-array'})},
  {binding:1,resource:copied.getGPU().createView({dimension:'2d-array'})}]});
 const pass=GPU.getEncoder().beginComputePass();pass.setPipeline(pipeline);pass.setBindGroup(0,group);
 pass.dispatchWorkgroups(shadows.size/8,shadows.size/8,shadows.count);pass.end();
 const files={},payloads=[];
 for(let i=0;i<shadows.count;i++){
  const bytes=Buffer.from((await readTexture(copied,{layer:i})).data);
  const values=new Float32Array(bytes.buffer,bytes.byteOffset,bytes.byteLength/4);
  let min=1,occupied=0;for(const v of values){if(!Number.isFinite(v)||v<0||v>1)throw new Error('Invalid depth');min=Math.min(min,v);if(v<1)occupied++;}
  payloads.push(bytes);files[i]={file:names[i],sha256:createHash('sha256').update(bytes).digest('hex'),min,occupied};
 }
 const error=await GPU.device.popErrorScope();if(error)throw new Error(error.message);
 for(let i=0;i<payloads.length;i++)writeFileSync(names[i],payloads[i],{flag:'wx'});
 const casters=[];scene.traverse(o=>{if(o.geometry&&o.castShadow)casters.push({name:o.name,
  vertices:o.geometry.attributes.position.count,indices:o.geometry.index?.count,
  color_draw_range:{...o.geometry.drawRange},instances:o.isInstancedMesh?o.count:(o.geometry.instanceCount??1),
  has_per_pass_callback:!!o.onBeforeRender,side:o.material.side,alpha_test:o.material.alphaTest,shadow_hook:o.material.shadow});});
 const u=ShadowUniforms.fields;
 writeFileSync(output+'.shadow-filter.wgsl',shadowModule.code,{flag:'wx'});
 writeFileSync(output+'.shadows.json',JSON.stringify({schema:'forge.tidewater.object-shadow-oracle',version:1,
  source_commit:'1438b1abfcaee3267092b75573014f4d9b4a983c',map_size:shadows.size,files,casters,
  cascades:shadows.cascades.map((c,i)=>({matrix:c.viewProj.elements,world:c.camera.matrixWorld.elements,
    view:c.camera.matrixWorldInverse.elements,projection:c.camera.projectionMatrix.elements,
    info:u.cascades.value[i].toArray(),blend:u.blend.value[i].toArray(),radius:c.radius})),
  bias:u.bias.value,normal_bias:shadows.normalBias,pcss_cascades:u.pcssCascades.value,
  depth_bias:2,depth_bias_slope:1.5,
  limitations:['fixed_pose_and_time','no_cloud_shadows_or_bounce','no_fish_props','not_native_shadow_generation']},null,2),{flag:'wx'});
}
