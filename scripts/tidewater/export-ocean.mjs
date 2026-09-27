// Numerical FFT timeline from the untouched pinned engine; no rendered-water verdict.
import {readFileSync,writeFileSync,mkdirSync,existsSync} from 'node:fs';
import {resolve,join,relative,isAbsolute} from 'node:path';
import {pathToFileURL} from 'node:url';
import {execFileSync} from 'node:child_process';
import {createHash} from 'node:crypto';
const [sourceArg,outputArg,sea='default']=process.argv.slice(2);
if(!outputArg||process.argv.length>5||!['default','storm'].includes(sea))throw Error('SOURCE NEW_DIRECTORY [default|storm] required');
const source=resolve(sourceArg),output=resolve(outputArg),pin='1438b1abfcaee3267092b75573014f4d9b4a983c';
const git=(...a)=>execFileSync('git',['-C',source,...a],{encoding:'utf8'}).trim();
if(git('rev-parse','HEAD')!==pin||git('status','--porcelain'))throw Error('Clean pinned source required');
const rel=relative(source,output);
if(existsSync(output)||(!rel.startsWith('..')&&!isAbsolute(rel)))throw Error('New output outside source required');
const at=p=>import(pathToFileURL(join(source,p)).href);
await at('test/headless.mjs');
const {GPU}=await at('src/engine/gpu/GPU.js');
const {FrameUniforms,G}=await at('src/engine/render/Frame.js');
const {OceanFFT}=await at('src/ocean/OceanFFT.js');
const {readBuffer,readTexture}=await at('src/engine/gpu/Readback.js');
await GPU.init({headless:true});GPU.device.pushErrorScope('validation');
const simulationOptions=sea==='storm'?{choppiness:1.2,local:{windSpeed:20,windDirection:25,fetch:900,spreadBlend:.85,swell:.05},swell:{scale:1,windSpeed:6,windDirection:5,fetch:1200,spreadBlend:1,swell:.9,shortWavesFade:.1}}:{};
const ocean=new OceanFFT(null,simulationOptions),payloads={},snapshots=[];
// AppUI SEA.Storm + whitecaps(): same controls, without instantiating the DOM UI.
if(sea==='storm'){ocean.foamBias.value=.5+.16+.01*12;ocean.foamDecay.value=.6-.35;}
const simulationFrame={foamBias:ocean.foamBias.value,foamDecay:ocean.foamDecay.value};
const saveParams=(name)=>{ocean.params._pack();payloads[name]=Buffer.from(new Uint8Array(ocean.params.data));};
saveParams('params-initial.bin');
// Exercise every active cascade count and changed weather independently of GPU outputs.
const parameterCases=[];
for(let cascades=1;cascades<=4;cascades++)for(const weather of [false,true]){
 const options={cascades,...(weather?{depth:17,choppiness:1.2,local:{windSpeed:20,windDirection:-73,fetch:900,spreadBlend:.7,swell:.1},swell:{scale:1,windSpeed:6,windDirection:5,fetch:1200,spreadBlend:1,swell:.9,shortWavesFade:.1}}:{})};
 const probe=new OceanFFT(null,options);probe.params._pack();
 const name=`params-c${cascades}-${weather?'storm':'default'}.bin`;
 payloads[name]=Buffer.from(probe.params.data);parameterCases.push({options,file:name});
}
for(let frame=1;frame<=120;frame++){
 GPU.beginFrame();G.dt.value=1/60;G.time.value=frame/60;FrameUniforms.fields.frameIndex.value=frame;
 ocean.update(1/60);GPU.submit();await GPU.queue.onSubmittedWorkDone();
 if(![1,60,120].includes(frame))continue;
 const prefix=`frame-${String(frame).padStart(3,'0')}`;
 saveParams(prefix+'-params.bin');FrameUniforms._pack();payloads[prefix+'-frame.bin']=Buffer.from(new Uint8Array(FrameUniforms.data));
 for(const [name,words] of [['h0',4],['waveData',4],['foam',1]])
  payloads[`${prefix}-${name}.f32.bin`]=Buffer.from(await readBuffer(ocean[name],256*256*4*words*4));
 for(const [name,texture] of [['displacement',ocean.displacementTexture],['derivatives',ocean.derivativeTexture]])
  for(let layer=0;layer<4;layer++)for(let mip=0;mip<=8;mip++)
   payloads[`${prefix}-${name}-c${layer}-m${mip}.rgba16.bin`]=Buffer.from((await readTexture(texture,{layer,mip})).data);
 snapshots.push({frame,time:ocean.time.value,dt:1/60,prefix});
}
for(const name of ['initSpectrum','conjugate','copyH0','row','column'])payloads[name+'.wgsl']=Buffer.from(ocean[name+'Kernel'].source);
for(const stage of ['A','B'])for(let i=0;i<2;i++)payloads[`mip${stage}-${i}.wgsl`]=Buffer.from(ocean['mipKernels'+stage][i].source);
payloads['sampling.wgsl']=Buffer.from(ocean.module.code);
const error=await GPU.device.popErrorScope();if(error)throw Error(error.message);
if(git('status','--porcelain'))throw Error('Source changed during export');
mkdirSync(output,{recursive:true});const files={};
for(const [name,data] of Object.entries(payloads)){writeFileSync(join(output,name),data,{flag:'wx'});files[name]=createHash('sha256').update(data).digest('hex');}
writeFileSync(join(output,'ocean.json'),JSON.stringify({schema:'forge.tidewater.ocean-probe',version:1,source_commit:pin,sea,simulation_options:simulationOptions,simulation_frame:simulationFrame,fft_size:256,cascades:4,sizes:ocean.sizes,snapshots,parameter_cases:parameterCases,files,exporter_sha256:createHash('sha256').update(readFileSync(new URL(import.meta.url))).digest('hex'),limitations:['fixed_120_steps_at_60_hz','no_shore_water_material_or_visual_parity','no_native_execution_verdict']},null,2),{flag:'wx'});
console.log(JSON.stringify({output,files:Object.keys(files).length,snapshots}));GPU.device.destroy();process.exit(0);
