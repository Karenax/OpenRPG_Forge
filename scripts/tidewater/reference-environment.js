// Diagnostic readback after the original App history; never calls App.frame.
import { GPU } from '/src/engine/gpu/GPU.js';
import { Texture } from '/src/engine/gpu/Texture.js';
import { readBuffer, readTexture } from '/src/engine/gpu/Readback.js';
import { G, FrameUniforms } from '/src/engine/render/Frame.js';

const encoded = data => {
  const bytes = new Uint8Array(data);
  let text = '';
  for (let offset=0;offset<bytes.length;offset+=8192) {
    text += String.fromCharCode(...bytes.subarray(offset,offset+8192));
  }
  return btoa(text);
};
const color = value => [value.r,value.g,value.b];
const clocks = app => ({
  global:G.time.value, ocean:app.fft.time.value,
  cloud:[...app.clouds.F.clock.value],
});

export async function acquire(app) {
  const before = clocks(app);
  const environment = app.environment;
  const cube = environment.texture;
  // Environment.js deliberately uses six roughness levels (128 down to 4),
  // rather than the full eight-level source-sky mip chain.
  if (cube.format!=='rgba16float' || cube.width!==128 || cube.height!==128 || cube.mipLevelCount!==6) {
    throw Error('Unexpected source environment cube layout');
  }
  await GPU.queue.onSubmittedWorkDone();
  GPU.device.pushErrorScope('validation');
  const files = {};
  let staging;
  try {
    files['irradiance-sh.f32.bin'] = encoded(await readBuffer(environment.sh[environment.front],144));
    // The original cube has no COPY_SRC. Identity load/store reads every mip
    // through its existing sampled view into a separate diagnostic texture.
    const shader = GPU.device.createShaderModule({code:`
@group(0) @binding(0) var source:texture_2d_array<f32>;
@group(0) @binding(1) var copied:texture_storage_2d_array<rgba16float,write>;
@compute @workgroup_size(8,8) fn main(@builtin(global_invocation_id) id:vec3u){
 if(any(id.xy>=textureDimensions(copied))||id.z>=6u){return;}
 textureStore(copied,vec2i(id.xy),i32(id.z),textureLoad(source,vec2i(id.xy),i32(id.z),0));
}`});
    const pipeline = GPU.device.createComputePipeline({layout:'auto',compute:{module:shader,entryPoint:'main'}});
    staging = new Texture({width:cube.width,height:cube.height,dimension:'cube',format:'rgba16float',
      mips:cube.mipLevelCount,usage:['sample','storage','copySrc']});
    const mips = [];
    for (let level=0;level<cube.mipLevelCount;level++) {
      const view = {dimension:'2d-array',baseMipLevel:level,mipLevelCount:1};
      const group = GPU.device.createBindGroup({layout:pipeline.getBindGroupLayout(0),entries:[
        {binding:0,resource:cube.getGPU().createView(view)},
        {binding:1,resource:staging.getGPU().createView(view)},
      ]});
      const size = Math.max(1,cube.width>>level);
      const pass = GPU.getEncoder().beginComputePass();
      pass.setPipeline(pipeline);pass.setBindGroup(0,group);
      pass.dispatchWorkgroups(Math.ceil(size/8),Math.ceil(size/8),6);pass.end();
      const faces = [];
      for (let face=0;face<6;face++) {
        const name = `specular-${level}-${face}.rgba16.bin`;
        const read = await readTexture(staging,{mip:level,layer:face});
        files[name] = encoded(read.data);faces.push(name);
      }
      mips.push({level,width:size,height:size,faces});
    }
    const after = clocks(app);
    if (JSON.stringify(before)!==JSON.stringify(after)) throw Error('Source animation advanced during readback');
    const error = await GPU.device.popErrorScope();
    if (error) throw Error(error.message);
    return {schema:'forge.tidewater.reference-environment',version:1,
      source_commit:'1438b1abfcaee3267092b75573014f4d9b4a983c',
      acquisition:'original_app_four_preparation_then_120_reference_frames',
      clocks_before:before,clocks_after:after,
      sun_direction:[G.sunDir.value.x,G.sunDir.value.y,G.sunDir.value.z],
      sun_color:color(G.sunColor.value),sky_irradiance:color(G.skyIrradiance.value),
      environment_intensity:FrameUniforms.fields.envIntensity.value,
      night:G.night.value,sh:'irradiance-sh.f32.bin',mips,files,
      gpu_validation_error:null,visual_parity:false};
  } finally {
    staging?.destroy();
  }
}
