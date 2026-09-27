// Independent original village albedo pass, without terrain/lighting/fish props.
import {readFileSync,writeFileSync,existsSync} from 'node:fs';
import {execFileSync} from 'node:child_process';
import {resolve,join} from 'node:path';
import {pathToFileURL} from 'node:url';
import {createHash} from 'node:crypto';
const [sourceArg,fixtureArg,outputArg]=process.argv.slice(2);
if(!sourceArg||!fixtureArg||!outputArg) throw new Error('SOURCE FIXTURE NEW_PNG required');
const source=resolve(sourceArg),fixturePath=resolve(fixtureArg),output=resolve(outputArg);
const PIN='1438b1abfcaee3267092b75573014f4d9b4a983c';
const git=(...args)=>execFileSync('git',['-C',source,...args],{encoding:'utf8'}).trim();
if(git('rev-parse','HEAD')!==PIN||git('status','--porcelain')) throw new Error('Clean pinned source required');
if(existsSync(output)||existsSync(output+'.json')) throw new Error('Refusing overwrite');
const fixtureBytes=readFileSync(join(fixturePath,'fixture.json'));
const fixture=JSON.parse(fixtureBytes);
if(fixture.source.commit!==PIN||!fixture.camera_pose) throw new Error('Pinned camera required');
const moduleAt=p=>import(pathToFileURL(join(source,p)).href);
const {writePNG}=await moduleAt('test/headless.mjs');
const {GPU}=await moduleAt('src/engine/gpu/GPU.js');
await GPU.init({headless:true});GPU.device.pushErrorScope('validation');
const E=await moduleAt('src/engine/index.js');
const {RenderTarget}=await moduleAt('src/engine/gpu/Texture.js');
const {readTexture}=await moduleAt('src/engine/gpu/Readback.js');
const {MeshRenderer}=await moduleAt('src/engine/render/MeshRenderer.js');
const {SunShadows}=await moduleAt('src/engine/render/Shadows.js');
const {G,FrameUniforms,setFrameCamera}=await moduleAt('src/engine/render/Frame.js');
const {TerrainData}=await moduleAt('src/world/TerrainData.js');
const {Village}=await moduleAt('src/world/Village.js');
const {Colliders}=await moduleAt('src/world/Colliders.js');
const scene=new E.Scene();
const village=new Village({scene,terrain:new TerrainData(),colliders:new Colliders()});
for(const child of [...village.group.children]) if(child.name==='village_fish') village.group.remove(child);
for(const m of Object.values(village.materials)){m.lit=false;m.output='r.color = vec4f(s.albedo,s.alpha);';}
const W=1920,H=1080,c=fixture.camera_pose;
const camera=new E.PerspectiveCamera(c.fov_degrees,W/H,.06,60000);
camera.position.set(...c.position);camera.quaternion.set(...c.rotation);camera.updateMatrixWorld();
G.time.value=2;G.dt.value=0;G.night.value=0;G.windSpeed.value=6;G.windDir.value.set(1,0);
FrameUniforms.fields.frameIndex.value=124;setFrameCamera(camera,W,H);
// sRGB attachment matches the native window transfer; material values stay linear.
const format='rgba8unorm-srgb';
const target=new RenderTarget(W,H,{colors:[format],depth:'depth32float',label:'village-albedo-oracle'});
const renderer=new MeshRenderer(),shadows=new SunShadows();
shadows.render(scene,renderer,shadows.update(camera,G.sunDir.value));setFrameCamera(camera,W,H);
renderer.render(scene,{camera,kind:'color',colorViews:[target.texture.view()],colorFormats:[format],
  clearColors:[[0,0,0,1]],depthView:target.depthTexture.view(),depthFormat:'depth32float',clearDepth:0});
GPU.submit();await GPU.queue.onSubmittedWorkDone();
const result=await readTexture(target.texture),rgba=new Uint8Array(result.data);
const error=await GPU.device.popErrorScope();if(error) throw new Error(error.message);
if(git('status','--porcelain')) throw new Error('Source changed');
writePNG(output,W,H,rgba);
writeFileSync(output+'.json',JSON.stringify({source_commit:PIN,
  fixture_sha256:createHash('sha256').update(fixtureBytes).digest('hex'),width:W,height:H,
  camera:c,renderer:'upstream_webgpu',purpose:'village_albedo_only',time:2,wind:[1,0,6],night:0,
  limitations:['no_lighting','fish_props_excluded','dynamic_groups_initial_pose','fixed_time']},null,2),{flag:'wx'});
console.log(JSON.stringify({output,status:'source_village_albedo_only'}));
GPU.device.destroy();process.exit(0);
