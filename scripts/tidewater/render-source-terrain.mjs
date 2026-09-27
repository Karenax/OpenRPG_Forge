// Independent pinned-source CDLOD coverage oracle, preserving original vertices.
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
if(fixture.source.commit!==PIN||!fixture.camera_pose) throw new Error('Pinned explicit camera required');
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
const {TerrainGPU}=await moduleAt('src/world/TerrainGPU.js');
const {Terrain}=await moduleAt('src/world/Terrain.js');
const {Village}=await moduleAt('src/world/Village.js');
const {Colliders}=await moduleAt('src/world/Colliders.js');
const terrain=new TerrainData();new Village({scene:new E.Scene(),terrain,colliders:new Colliders()});
const hash=b=>createHash('sha256').update(b).digest('hex');
const heights=Buffer.from(terrain.heights.buffer,terrain.heights.byteOffset,terrain.heights.byteLength);
if(hash(heights)!==fixture.files['heights.bin']) throw new Error('Height source differs');
const scene=new E.Scene(),gpu=new TerrainGPU(terrain);
const terrainView=new Terrain({scene,terrainData:terrain,terrainGPU:gpu});
terrainView.material.lit=false;terrainView.material.output='r.color = vec4f(1.0);';
const W=1920,H=1080,c=fixture.camera_pose;
// Original reference pose; native f32 conversion differences remain observable.
const camera=new E.PerspectiveCamera(c.fov_degrees,W/H,.06,60000);
camera.position.set(...c.position);camera.quaternion.set(...c.rotation);camera.updateMatrixWorld();
G.time.value=2;G.dt.value=0;G.windSpeed.value=6;G.windDir.value.set(1,0);
FrameUniforms.fields.frameIndex.value=124;
terrainView.update(camera);setFrameCamera(camera,W,H);
const target=new RenderTarget(W,H,{colors:['rgba8unorm'],depth:'depth32float',label:'terrain-coverage-oracle'});
const renderer=new MeshRenderer(),shadows=new SunShadows();
shadows.render(scene,renderer,shadows.update(camera,G.sunDir.value));setFrameCamera(camera,W,H);
renderer.render(scene,{camera,kind:'color',colorViews:[target.texture.view()],colorFormats:['rgba8unorm'],
  clearColors:[[0,0,0,1]],depthView:target.depthTexture.view(),depthFormat:'depth32float',clearDepth:0});
GPU.submit();await GPU.queue.onSubmittedWorkDone();
const result=await readTexture(target.texture),rgba=new Uint8Array(result.data);
let covered=0;for(let i=0;i<rgba.length;i+=4) if(rgba[i]>127) covered++;
if(covered<100||covered>=W*H) throw new Error(`Invalid coverage ${covered}`);
const error=await GPU.device.popErrorScope();if(error) throw new Error(error.message);
if(git('status','--porcelain')) throw new Error('Source changed');
writePNG(output,W,H,rgba);
writeFileSync(output+'.json',JSON.stringify({source_commit:PIN,fixture_sha256:hash(fixtureBytes),
  nodes:terrainView.lod.count,covered_pixels:covered,width:W,height:H,
  camera:c,renderer:'upstream_webgpu',purpose:'terrain_geometry_coverage_only',
  adapter:{vendor:GPU.adapter.info.vendor,description:GPU.adapter.info.description},
  limitations:['single_pose','lighting_replaced_with_coverage','native_camera_precision_may_differ']},null,2),{flag:'wx'});
console.log(JSON.stringify({output,covered_pixels:covered,nodes:terrainView.lod.count}));
GPU.device.destroy();process.exit(0);
