// Acquisition of actual upstream uniforms and pixels; no Forge parity verdict.
import { writeFile, mkdir } from 'node:fs/promises';
import { join } from 'node:path';
import { createHash } from 'node:crypto';
const [endpoint, url, output, option] = process.argv.slice(2);
if (option && !['--environment','--environment-clouds','--all-effects'].includes(option)) throw Error('Unknown acquisition option');
if (process.argv.length>6) throw Error('Too many acquisition arguments');
if (!endpoint || !url || !output) throw Error('endpoint, URL and fresh output required');
const target = await fetch(`${endpoint}/json/new?about:blank`, {method:'PUT'}).then(r=>r.json());
const socket = new WebSocket(target.webSocketDebuggerUrl);
await new Promise((resolve,reject)=>{socket.addEventListener('open',resolve,{once:true});socket.addEventListener('error',reject,{once:true});});
let id=0;
const pending=new Map(), errors=[];
socket.addEventListener('message', event=>{
  const message=JSON.parse(event.data);
  if(message.id) pending.get(message.id)?.(message);
  else if(message.method==='Runtime.exceptionThrown') errors.push(message.params.exceptionDetails);
  else if(message.method==='Log.entryAdded' && message.params.entry.level==='error') errors.push(message.params.entry);
});
function send(method,params={}) {
  const key=++id;
  return new Promise((resolve,reject)=>{
    const timer=setTimeout(()=>{pending.delete(key);reject(Error(`CDP timeout: ${method}`));},120000);
    pending.set(key,message=>{clearTimeout(timer);pending.delete(key);message.error?reject(Error(JSON.stringify(message.error))):resolve(message.result);});
    socket.send(JSON.stringify({id:key,method,params}));
  });
}
async function evaluate(expression) {
  const result=await send('Runtime.evaluate',{expression,awaitPromise:true,returnByValue:true});
  if(result.exceptionDetails) throw Error(JSON.stringify(result.exceptionDetails));
  return result.result.value;
}
try {
  await send('Page.enable'); await send('Runtime.enable'); await send('Log.enable');
  await send('Emulation.setDeviceMetricsOverride',{width:1920,height:1080,deviceScaleFactor:1,mobile:false});
  await send('Page.navigate',{url});
  let metadata;
  const deadline=Date.now()+600000;
  while(Date.now()<deadline) {
    metadata=await evaluate('window.forgeReference ?? null');
    if(metadata?.status==='failed') throw Error(metadata.message);
    if(metadata?.status==='ready_reference_only') break;
    await new Promise(resolve=>setTimeout(resolve,1000));
  }
  if(metadata?.status!=='ready_reference_only') throw Error('Source acquisition did not become ready');
  if(metadata.source_commit!=='1438b1abfcaee3267092b75573014f4d9b4a983c' || metadata.view!=='village') throw Error('Wrong source or view');
  const clocks=metadata.animation_clocks;
  for(const name of ['global_time_s','global_dt_s','ocean_time_s','ocean_time_scale','wind_speed']) {
    if(typeof clocks?.[name]!=='number' || !Number.isFinite(clocks[name])) throw Error(`Missing actual clock: ${name}`);
  }
  if(!Array.isArray(clocks.cloud_clock) || clocks.cloud_clock.length!==4 || !clocks.cloud_clock.every(Number.isFinite)) throw Error('Actual cloud clock missing');
  if(!Array.isArray(clocks.vegetation_gust_offset) || clocks.vegetation_gust_offset.length!==2 || !clocks.vegetation_gust_offset.every(Number.isFinite)) throw Error('Actual vegetation gust offset missing');
  const history=metadata.frame_history;
  if(!Array.isArray(history) || history.length!==124) throw Error('Complete source frame history missing');
  for(let i=0;i<history.length;i++) {
    const frame=history[i];
    if(frame.phase!==(i<4?'source-preparation':'reference-warmup') || frame.dt!==1/60) throw Error('Source preparation sequence differs');
    for(const side of ['before','after']) {
      if(!Number.isFinite(frame[side]?.global_time_s) || !Number.isInteger(frame[side]?.gpu_frame)) throw Error('Source frame observation invalid');
    }
    if(Math.abs(frame.after.global_time_s-frame.before.global_time_s-frame.dt)>1e-12 || frame.after.gpu_frame!==frame.before.gpu_frame+1) throw Error('Source transaction did not advance coherently');
    if(!Number.isFinite(frame.after.ocean_time_s) || !Number.isFinite(frame.after.ocean_time_scale)) throw Error('Source ocean history invalid');
    if(!Array.isArray(frame.after.cloud_clock) || frame.after.cloud_clock.length!==4 || !frame.after.cloud_clock.every(Number.isFinite)) throw Error('Source cloud history invalid');
    for(const name of ['position','right','up','forward']) {
      if(!Array.isArray(frame.cloud_camera?.[name]) || frame.cloud_camera[name].length!==3 || !frame.cloud_camera[name].every(Number.isFinite)) throw Error('Source cloud camera missing');
    }
    if(i && Math.abs(frame.after.ocean_time_s-history[i-1].after.ocean_time_s-frame.dt*frame.after.ocean_time_scale)>1e-12) throw Error('Source ocean sequence discontinuous');
    if(frame.cloud_input?.dt!==frame.dt || typeof frame.cloud_input?.underwater!=='boolean') throw Error('Actual cloud producer input missing');
    if(typeof frame.cloud_camera_f64le!=='string' || Buffer.from(frame.cloud_camera_f64le,'base64').length!==120) throw Error('Exact source camera values missing');
    for(const [name,length] of Object.entries({frame:544,settings:208,public:80,global:720,atmosphere:64})) {
      const encoded=frame.cloud_gpu_blocks?.[name];
      if(typeof encoded!=='string' || Buffer.from(encoded,'base64').length!==length) throw Error(`Effective source GPU block missing: ${name}`);
    }
  }
  if(history[4].before.global_time_s!==0) throw Error('Global reset was not observed');
  const environment=await evaluate(`(async()=>{const {GPU}=await import('/src/engine/gpu/GPU.js');const info=GPU.adapter.info;return {viewport:[innerWidth,innerHeight],dpr:devicePixelRatio,canvas:[document.querySelector('canvas').width,document.querySelector('canvas').height],userAgent:navigator.userAgent,adapter:{vendor:info.vendor,architecture:info.architecture,device:info.device,description:info.description}};})()`);
  if(JSON.stringify(environment.canvas)!=='[1920,1080]' || JSON.stringify(metadata.output)!=='[1920,1080]') throw Error('Wrong acquisition dimensions');
  const image=await send('Page.captureScreenshot',{format:'png',fromSurface:true,captureBeyondViewport:false});
  const bytes=Buffer.from(image.data,'base64');
  if(bytes.subarray(0,8).toString('hex')!=='89504e470d0a1a0a' || bytes.readUInt32BE(16)!==1920 || bytes.readUInt32BE(20)!==1080) throw Error('Wrong PNG signature or dimensions');
  await writeFile(join(output,'village.png'),bytes,{flag:'wx'});
  await writeFile(join(output,'village.json'),JSON.stringify(metadata,null,2),{flag:'wx'});
  if (option==='--environment' || option==='--environment-clouds' || option==='--all-effects') {
    const acquired = await evaluate('window.forgeAcquireEnvironment()');
    if (acquired.source_commit!==metadata.source_commit || acquired.gpu_validation_error!==null ||
        JSON.stringify(acquired.clocks_before)!==JSON.stringify(acquired.clocks_after)) throw Error('Environment acquisition failed');
    const folder = join(output,'environment');
    await mkdir(folder);
    const pins = {};
    const names = ['irradiance-sh.f32.bin'];
    for (let mip=0;mip<6;mip++) for (let face=0;face<6;face++) names.push(`specular-${mip}-${face}.rgba16.bin`);
    if (JSON.stringify(Object.keys(acquired.files).sort())!==JSON.stringify(names.sort())) throw Error('Environment files differ');
    for (const name of names) {
      const bytes = Buffer.from(acquired.files[name],'base64');
      const mip = name.startsWith('specular-') ? Number(name.split('-')[1]) : null;
      const size = mip===null ? 144 : Math.max(1,128>>mip)**2*8;
      if (bytes.length!==size) throw Error(`Environment size differs: ${name}`);
      await writeFile(join(folder,name),bytes,{flag:'wx'});
      pins[name] = createHash('sha256').update(bytes).digest('hex');
    }
    acquired.files = pins;
    acquired.reference_sha256 = createHash('sha256').update(JSON.stringify(metadata,null,2)).digest('hex');
    await writeFile(join(folder,'environment.json'),JSON.stringify(acquired,null,2),{flag:'wx'});
  }
  if (option==='--environment-clouds' || option==='--all-effects') {
    const acquired = await evaluate('window.forgeAcquireCloudOutput()');
    if (acquired.source_commit!==metadata.source_commit || acquired.gpu_validation_error!==null ||
        JSON.stringify(acquired.clocks_before)!==JSON.stringify(acquired.clocks_after)) throw Error('Cloud output acquisition failed');
    const sizes = {
      'weather.rgba8.bin':[1024,1024,4,'rgba8unorm'],'bounds.rgba8.bin':[64,64,4,'rgba8unorm'],
      'shadow.r32.bin':[256,256,4,'r32float'],'radiance.rgba16.bin':[240,135,8,'rgba16float'],
      'metadata.rgba16.bin':[240,135,8,'rgba16float'],'panorama.rgba16.bin':[512,160,8,'rgba16float'],
    };
    for (const kind of ['color','meta']) for (let index=0;index<2;index++) sizes[`${kind}${index}.rgba16.bin`]=[960,540,8,'rgba16float'];
    if (JSON.stringify(Object.keys(acquired.files).sort())!==JSON.stringify(Object.keys(sizes).sort())) throw Error('Cloud output file set differs');
    const folder = join(output,'cloud-gpu');await mkdir(folder);
    const pins = {};
    for (const [name,[width,height,bpp,format]] of Object.entries(sizes)) {
      const description = acquired.descriptions[name];
      const bytes = Buffer.from(acquired.files[name],'base64');
      if (description.width!==width || description.height!==height || description.format!==format || bytes.length!==width*height*bpp) throw Error(`Cloud output layout differs: ${name}`);
      await writeFile(join(folder,name),bytes,{flag:'wx'});
      pins[name] = createHash('sha256').update(bytes).digest('hex');
    }
    acquired.files = pins;
    acquired.reference_sha256 = createHash('sha256').update(JSON.stringify(metadata,null,2)).digest('hex');
    await writeFile(join(folder,'cloud-output.json'),JSON.stringify(acquired,null,2),{flag:'wx'});
  }
  if (option==='--all-effects') {
    for (const frame of history) {
      if (typeof frame.ocean_gpu_params!=='string' || Buffer.from(frame.ocean_gpu_params,'base64').length!==224) throw Error('Effective ocean parameters missing');
    }
    const acquired = await evaluate('window.forgeAcquireOceanOutput()');
    if (acquired.source_commit!==metadata.source_commit || acquired.gpu_validation_error!==null ||
        JSON.stringify(acquired.clocks_before)!==JSON.stringify(acquired.clocks_after)) throw Error('Ocean acquisition failed');
    const sizes = {'h0.f32.bin':256*256*4*16,'waveData.f32.bin':256*256*4*16,'foam.f32.bin':256*256*4*4};
    for (const kind of ['displacement','derivatives']) for(let layer=0;layer<4;layer++) for(let mip=0;mip<9;mip++) {
      sizes[`${kind}-c${layer}-m${mip}.rgba16.bin`] = (256>>mip)**2*8;
    }
    const shaders = ['initSpectrum','conjugate','copyH0','row','column','mipA-0','mipA-1','mipB-0','mipB-1','sampling'].map(name=>name+'.wgsl');
    const names = [...Object.keys(sizes),...shaders];
    if(JSON.stringify(Object.keys(acquired.files).sort())!==JSON.stringify(names.sort())) throw Error('Ocean output file set differs');
    const folder=join(output,'ocean-gpu');await mkdir(folder);const pins={};
    for(const name of names) {
      const bytes=Buffer.from(acquired.files[name],'base64');
      if(sizes[name]!==undefined ? bytes.length!==sizes[name] : bytes.length===0 || bytes.length>1000000) throw Error(`Ocean resource size differs: ${name}`);
      await writeFile(join(folder,name),bytes,{flag:'wx'});
      pins[name]=createHash('sha256').update(bytes).digest('hex');
    }
    for(let index=0;index<history.length;index++) {
      const name=`ocean-step${String(index+1).padStart(3,'0')}-params.bin`;
      const bytes=Buffer.from(history[index].ocean_gpu_params,'base64');
      await writeFile(join(folder,name),bytes,{flag:'wx'});
      pins[name]=createHash('sha256').update(bytes).digest('hex');
    }
    acquired.files=pins;
    acquired.reference_sha256=createHash('sha256').update(JSON.stringify(metadata,null,2)).digest('hex');
    await writeFile(join(folder,'ocean-output.json'),JSON.stringify(acquired,null,2),{flag:'wx'});
  }
  // Vite has no favicon; retain that HTTP 404 without treating it as a render failure.
  const blockingErrors=errors.filter(error=>!(error.source==='network' && error.text?.includes('404 (Not Found)') && error.url===new URL('/favicon.ico',url).href));
  await writeFile(join(output,'acquisition.json'),JSON.stringify({schema:'forge.tidewater.capture-session',version:1,viewport_set_before_load:true,environment,errors,blocking_errors:blockingErrors,visual_parity:false,temporal_replay_qualified:false},null,2),{flag:'wx'});
  if(blockingErrors.length) throw Error('Browser reported errors; retained acquisition is unqualified');
  process.stdout.write(JSON.stringify({status:'captured_reference_only',animation_clocks:clocks})+'\n');
} finally {
  await new Promise(resolve=>{const timer=setTimeout(resolve,5000);socket.addEventListener('close',()=>{clearTimeout(timer);resolve();},{once:true});socket.close();});
  await fetch(`${endpoint}/json/close/${target.id}`).catch(()=>{});
}
