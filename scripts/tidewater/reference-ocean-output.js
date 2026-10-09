// Acquire the original App FFT history's final state, never a cold replacement.
import { GPU } from '/src/engine/gpu/GPU.js';
import { readBuffer, readTexture } from '/src/engine/gpu/Readback.js';
import { G } from '/src/engine/render/Frame.js';
const encoded = data => {
  const bytes = new Uint8Array(data);let text='';
  for(let i=0;i<bytes.length;i+=8192) text+=String.fromCharCode(...bytes.subarray(i,i+8192));
  return btoa(text);
};
const clocks = app => ({global:G.time.value,ocean:app.fft.time.value,cloud:[...app.clouds.F.clock.value]});
export async function acquire(app) {
  const ocean=app.fft,before=clocks(app),files={},descriptions={};
  if(ocean.cascades!==4 || ocean.timeScale!==1) throw Error('Unexpected acquired ocean layout or speed');
  await GPU.queue.onSubmittedWorkDone();GPU.device.pushErrorScope('validation');
  for(const [name,words] of [['h0',4],['waveData',4],['foam',1]]) {
    const filename=`${name}.f32.bin`,length=256*256*4*words*4;
    files[filename]=encoded(await readBuffer(ocean[name],length));
    descriptions[filename]={bytes:length,format:'f32'};
  }
  for(const [name,texture] of [['displacement',ocean.displacementTexture],['derivatives',ocean.derivativeTexture]]) {
    if(texture.width!==256 || texture.height!==256 || texture.mipLevelCount!==9 || texture.format!=='rgba16float') throw Error('Unexpected acquired FFT texture');
    for(let layer=0;layer<4;layer++) for(let mip=0;mip<9;mip++) {
      const filename=`${name}-c${layer}-m${mip}.rgba16.bin`;
      const read=await readTexture(texture,{layer,mip});
      files[filename]=encoded(read.data);
      descriptions[filename]={width:read.width,height:read.height,layer,mip,format:'rgba16float'};
    }
  }
  for(const name of ['initSpectrum','conjugate','copyH0','row','column']) files[name+'.wgsl']=encoded(new TextEncoder().encode(ocean[name+'Kernel'].source));
  for(const stage of ['A','B']) for(let i=0;i<2;i++) files[`mip${stage}-${i}.wgsl`]=encoded(new TextEncoder().encode(ocean['mipKernels'+stage][i].source));
  files['sampling.wgsl']=encoded(new TextEncoder().encode(ocean.module.code));
  const after=clocks(app);
  if(JSON.stringify(before)!==JSON.stringify(after)) throw Error('Source animation advanced during FFT readback');
  const error=await GPU.device.popErrorScope();if(error) throw Error(error.message);
  return {schema:'forge.tidewater.reference-ocean-output',version:1,
    source_commit:'1438b1abfcaee3267092b75573014f4d9b4a983c',
    acquisition:'original_app_four_preparation_then_120_reference_frames',
    clocks_before:before,clocks_after:after,cascades:4,fft_size:256,sizes:ocean.sizes,
    files,descriptions,gpu_validation_error:null,visual_parity:false};
}
