// CPU-only oracle: execute the pinned controller with dispatches intercepted.
import {mkdirSync,writeFileSync,existsSync} from 'node:fs';
import {resolve,join} from 'node:path';
import {pathToFileURL} from 'node:url';
import {execFileSync} from 'node:child_process';
import {createHash} from 'node:crypto';
const [sourceArg,outputArg]=process.argv.slice(2);
if(!sourceArg||!outputArg||process.argv.length!==4)throw Error('SOURCE NEW_DIRECTORY');
const source=resolve(sourceArg),output=resolve(outputArg),pin='1438b1abfcaee3267092b75573014f4d9b4a983c';
const git=(...a)=>execFileSync('git',['-C',source,...a],{encoding:'utf8'}).trim();
if(git('rev-parse','HEAD')!==pin||git('status','--porcelain')||existsSync(output)||output.startsWith(source))throw Error('Clean pinned source and new external output required');
const moduleAt=p=>import(pathToFileURL(join(source,p)).href);
await moduleAt('test/headless.mjs');
const {GPU}=await moduleAt('src/engine/gpu/GPU.js');await GPU.init({headless:true});
const {Atmosphere}=await moduleAt('src/sky/Atmosphere.js');
const {SkyProClouds}=await moduleAt('src/sky/SkyProClouds.js');
const {G}=await moduleAt('src/engine/render/Frame.js');
const {PerspectiveCamera}=await moduleAt('src/engine/index.js');
const fetchBefore=globalThis.fetch;
// Noise is not needed by CPU scheduling. No compute dispatch or asset fetch is needed.
globalThis.fetch=()=>new Promise(()=>{});
const clouds=new SkyProClouds(null,new Atmosphere());globalThis.fetch=fetchBefore;
let dispatches=[];
for(const name of ['weather','bounds','shadow','trace','temporal','pano'])clouds[name+'Kernel'].dispatch=groups=>dispatches.push({name,groups});
const camera=new PerspectiveCamera(62,16/9,.1,10000);camera.position.set(42,14,-90);camera.rotation.set(-.2,Math.PI,0);
clouds.outputSize={x:1920,y:1080};G.sunDir.value.set(-.886125497221313,.45498452073767237,.08815151197450805);G.windDir.value.set(1,0);
mkdirSync(output,{recursive:true});const frames=[],files={};
for(let step=1;step<=32;step++){
 let dt=1/60,action='advance';
 if(step===4){camera.position.x+=5;action='small_move';}
 if(step===5){camera.position.x+=1500;action='teleport';}
 if(step===7){camera.rotation.y+=1.5;action='turn';}
 if(step===9){camera.fov=45;action='zoom';}
 if(step===11){clouds.coverage.value=.85;action='coverage';}
 if(step===13){G.sunDir.value.set(0,1,0);action='light';}
 if(step===15){G.windDir.value.set(-2,3);action='wind';}
 if(step===17){G.cameraUnderwater.value=1;action='underwater';}
 if(step===19){G.cameraUnderwater.value=0;action='surface';}
 if(step===21){clouds.outputSize={x:1280,y:720};camera.aspect=1280/720;action='resize';}
 if(step===23){clouds.resolutionScale=.75;action='scale';}
 if(step===25){dt=2;action='long_dt';}
 if(step===26){dt=-1;action='negative_dt';}
 if(step===27){G.windDir.value.set(0,0);action='zero_wind';}
 if(step===29){clouds.resetHistory();action='reset';}
 if(step===31){clouds.invalidate();action='invalidate';}
 const record={action,camera:{position:{...camera.position},quaternion:{...camera.quaternion},fov:camera.fov,aspect:camera.aspect,zoom:camera.zoom},output:[clouds.outputSize.x,clouds.outputSize.y],resolution_scale:clouds.resolutionScale,cloud_coverage:clouds.coverage.value,wind:{direction:[G.windDir.value.x,G.windDir.value.y]},sun_direction:[G.sunDir.value.x,G.sunDir.value.y,G.sunDir.value.z],underwater:G.cameraUnderwater.value>.5,dt};
 dispatches=[];clouds.update(dt,camera);record.dispatches=dispatches;record.view_index=clouds.colors.indexOf(clouds.viewTex);
 for(const [kind,block] of [['frame',clouds.frameBlock],['settings',clouds.settingsBlock],['public',clouds.params]]){
  block._pack();const data=Buffer.from(new Uint8Array(block.data)),name=`cloud-step${String(step).padStart(3,'0')}-${kind}.bin`;
  writeFileSync(join(output,name),data);files[name]=createHash('sha256').update(data).digest('hex');
 }
 frames.push(record);
}
writeFileSync(join(output,'controller.json'),JSON.stringify({source_commit:pin,controller_frames:frames,files,limits:['CPU update only; GPU dispatches intercepted','no rendering qualification']},null,2));
if(git('status','--porcelain'))throw Error('Source changed');
console.log(JSON.stringify({output,steps:frames.length,files:Object.keys(files).length}));GPU.device.destroy();process.exit(0);
