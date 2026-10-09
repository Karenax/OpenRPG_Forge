// Read the original App's final cloud textures without changing their usage,
// allocations, uniforms or frame history.
import { GPU } from '/src/engine/gpu/GPU.js';
import { Texture } from '/src/engine/gpu/Texture.js';
import { readTexture } from '/src/engine/gpu/Readback.js';
import { G } from '/src/engine/render/Frame.js';

const encoded = data => {
  const bytes = new Uint8Array(data);
  let text = '';
  for (let offset=0;offset<bytes.length;offset+=8192) text+=String.fromCharCode(...bytes.subarray(offset,offset+8192));
  return btoa(text);
};
const clock = app => ({global:G.time.value,ocean:app.fft.time.value,cloud:[...app.clouds.F.clock.value]});

export async function acquire(app) {
  const before = clock(app);
  const c = app.clouds;
  const textures = {weather:c.weatherMap,bounds:c.weatherBounds,shadow:c.shadowMap,
    radiance:c.source,metadata:c.sourceMeta,color0:c.colors[0],meta0:c.metas[0],
    color1:c.colors[1],meta1:c.metas[1],panorama:c.panorama};
  const files = {}, descriptions = {};
  await GPU.queue.onSubmittedWorkDone();
  GPU.device.pushErrorScope('validation');
  for (const [name,source] of Object.entries(textures)) {
    if (!['rgba8unorm','rgba16float','r32float'].includes(source.format)) throw Error(`Unknown cloud texture format: ${name}`);
    let staging;
    try {
      staging = new Texture({width:source.width,height:source.height,format:source.format,
        usage:['sample','storage','copySrc']});
      const module = GPU.device.createShaderModule({code:`
@group(0) @binding(0) var original:texture_2d<f32>;
@group(0) @binding(1) var copied:texture_storage_2d<${source.format},write>;
@compute @workgroup_size(8,8) fn main(@builtin(global_invocation_id) id:vec3u){
 if(any(id.xy>=textureDimensions(copied))){return;}
 textureStore(copied,vec2i(id.xy),textureLoad(original,vec2i(id.xy),0));
}`});
      // r32float does not require optional float32 filtering for textureLoad.
      const layout = GPU.device.createBindGroupLayout({entries:[
        {binding:0,visibility:GPUShaderStage.COMPUTE,texture:{sampleType:'unfilterable-float'}},
        {binding:1,visibility:GPUShaderStage.COMPUTE,storageTexture:{access:'write-only',format:source.format}},
      ]});
      const pipeline = GPU.device.createComputePipeline({layout:GPU.device.createPipelineLayout({bindGroupLayouts:[layout]}),
        compute:{module,entryPoint:'main'}});
      const group = GPU.device.createBindGroup({layout,entries:[
        {binding:0,resource:source.getGPU().createView()},
        {binding:1,resource:staging.getGPU().createView()},
      ]});
      const pass = GPU.getEncoder().beginComputePass();
      pass.setPipeline(pipeline);pass.setBindGroup(0,group);
      pass.dispatchWorkgroups(Math.ceil(source.width/8),Math.ceil(source.height/8));pass.end();
      const suffix = {rgba8unorm:'rgba8',rgba16float:'rgba16',r32float:'r32'}[source.format];
      const filename = `${name}.${suffix}.bin`;
      const read = await readTexture(staging);
      files[filename] = encoded(read.data);
      descriptions[filename] = {width:source.width,height:source.height,format:source.format};
    } finally {staging?.destroy();}
  }
  const after = clock(app);
  if (JSON.stringify(before)!==JSON.stringify(after)) throw Error('Source animation advanced during cloud readback');
  const error = await GPU.device.popErrorScope();
  if (error) throw Error(error.message);
  return {schema:'forge.tidewater.reference-cloud-output',version:1,
    source_commit:'1438b1abfcaee3267092b75573014f4d9b4a983c',
    acquisition:'original_app_four_preparation_then_120_reference_frames',
    clocks_before:before,clocks_after:after,files,descriptions,
    final_view_index:c.colors.indexOf(c.viewTex),gpu_validation_error:null,visual_parity:false};
}
