// Pinned upstream GPU oracle for deformation + coverage; not scene lighting.
import {readFileSync,writeFileSync,existsSync} from 'node:fs';
import {execFileSync} from 'node:child_process';
import {resolve,join} from 'node:path';
import {pathToFileURL} from 'node:url';
const [sourceArg,fixtureArg,outputArg]=process.argv.slice(2);
if(!sourceArg||!fixtureArg||!outputArg) throw new Error('SOURCE FIXTURE NEW_PNG required');
const source=resolve(sourceArg),fixturePath=resolve(fixtureArg),output=resolve(outputArg);
const PIN='1438b1abfcaee3267092b75573014f4d9b4a983c';
const git=(...args)=>execFileSync('git',['-C',source,...args],{encoding:'utf8'}).trim();
if(git('rev-parse','HEAD')!==PIN||git('status','--porcelain')) throw new Error('Source must be clean and pinned');
if(existsSync(output)||existsSync(output+'.json')) throw new Error('Refusing output overwrite');
const fixture=JSON.parse(readFileSync(join(fixturePath,'fixture.json'),'utf8'));
if(fixture.source.commit!==PIN) throw new Error('Wrong fixture source');
const moduleAt=path=>import(pathToFileURL(join(source,path)).href);
const {writePNG}=await moduleAt('test/headless.mjs');
const {GPU}=await moduleAt('src/engine/gpu/GPU.js');
await GPU.init({headless:true});
GPU.device.pushErrorScope('validation');
const E=await moduleAt('src/engine/index.js');
const {RenderTarget}=await moduleAt('src/engine/gpu/Texture.js');
const {readTexture}=await moduleAt('src/engine/gpu/Readback.js');
const {MeshRenderer}=await moduleAt('src/engine/render/MeshRenderer.js');
const {SunShadows}=await moduleAt('src/engine/render/Shadows.js');
const {G,FrameUniforms,setFrameCamera}=await moduleAt('src/engine/render/Frame.js');
const {VegInstances}=await moduleAt('src/world/vegetation/InstanceLOD.js');
const {vegParams}=await moduleAt('src/world/vegetation/VegNodes.js');
const geometryFns=await moduleAt('src/world/vegetation/PlantGeometry.js');
const {createPlantLeafMaterial,createCanopyMaterial}=await moduleAt('src/world/vegetation/VegMaterials.js');
const {LeafAtlas}=await moduleAt('src/world/vegetation/LeafTextures.js');
const record=fixture.instance;
// Keep the original source record/matrix math. The native adapter reduces its
// parameters to f32; any resulting coverage difference must remain observable.
let instances=new VegInstances([record]);
const names={palms:'buildPalmNear',trees:'buildTreeNear',shrubs:'buildShrubNear'};
const far=fixture.lod==='far';
let vegetation;
if(far){
  const {TerrainData}=await moduleAt('src/world/TerrainData.js');
  const {Village}=await moduleAt('src/world/Village.js');
  const {Colliders}=await moduleAt('src/world/Colliders.js');
  const {Vegetation}=await moduleAt('src/world/Vegetation.js');
  const terrain=new TerrainData(),scene=new E.Scene();
  const village=new Village({scene,terrain,colliders:new Colliders()});
  vegetation=new Vegetation({scene,terrain,village});
  if(fixture.species==='canopy_batch') instances=vegetation.canopy.inst;
}
const geometry=far?vegetation.canopy.far.meshes[0].geometry:geometryFns[names[fixture.species]]().geometry;
geometry.setAttribute('iPos',new E.InstancedBufferAttribute(instances.iPos,4));
geometry.setAttribute('iDat',new E.InstancedBufferAttribute(instances.iDat,4));
const atlas=new LeafAtlas(); atlas.bake();
const material=far?vegetation.materials[2]:(fixture.species==='palms'?createPlantLeafMaterial():createCanopyMaterial(atlas));
material.lit=false;
material.output='r.color = vec4f(1.0);';
const mesh=new E.InstancedMesh(geometry,material,instances.count);
mesh.instanceMatrix=new E.InstancedBufferAttribute(instances.matrices,16);
mesh.count=instances.count; mesh.frustumCulled=false; mesh.drawParams=[0,10000,10001];
const scene=new E.Scene(); scene.add(mesh);
const W=1920,H=1080;
const camera=new E.PerspectiveCamera(45,W/H,0.1,20000);
const f=Math.fround;
const offset=fixture.camera_offset||[15,8,19];
camera.position.set(f(f(record.x)+offset[0]),f(f(record.y)+offset[1]),f(f(record.z)+offset[2]));
camera.lookAt(f(record.x),f(f(record.y)+6),f(record.z));
camera.updateMatrixWorld();
vegParams.fields.camPos.value.copy(camera.position);
vegParams.fields.gustOffset.value.set(0,0);
G.time.value=2;
G.windSpeed.value=6;
G.windDir.value.set(1,0);
FrameUniforms.fields.frameIndex.value=124;
setFrameCamera(camera,W,H);
const target=new RenderTarget(W,H,{colors:['rgba8unorm'],depth:'depth32float',label:'plant-coverage-oracle'});
const renderer=new MeshRenderer();
// The upstream mesh template declares shadow resources even for an unlit
// coverage material. Initialize its own resources; no replacement shadow map.
const shadows=new SunShadows();
if(far){vegetation.leafAtlas.bake();vegetation.atlas.bake();}
shadows.render(scene,renderer,shadows.update(camera,G.sunDir.value));
setFrameCamera(camera,W,H);
renderer.render(scene,{camera,kind:'color',colorViews:[target.texture.view()],colorFormats:['rgba8unorm'],
  clearColors:[[0,0,0,1]],depthView:target.depthTexture.view(),depthFormat:'depth32float',clearDepth:0});
GPU.submit();
await GPU.queue.onSubmittedWorkDone();
const result=await readTexture(target.texture);
const rgba=new Uint8Array(result.data);
let covered=0;
for(let i=0;i<rgba.length;i+=4) if(rgba[i]>127) covered++;
if(covered<100||covered>W*H/2) throw new Error(`Invalid coverage ${covered}`);
const error=await GPU.device.popErrorScope();
if(error) throw new Error(error.message);
writePNG(output,W,H,rgba);
writeFileSync(output+'.json',JSON.stringify({source_commit:PIN,species:fixture.species,
  source_id:record.source_id,lod:fixture.lod||'near',covered_pixels:covered,width:W,height:H,time:2,frame:124,
  camera:{position:camera.position,quaternion:camera.quaternion,fov:45},
  purpose:'source_geometry_alpha_oracle_only',renderer:'upstream_webgpu',
  adapter:{vendor:GPU.adapter.info.vendor,device:GPU.adapter.info.device,description:GPU.adapter.info.description},
  limitations:['lighting_replaced_with_coverage','single_fixed_time','camera_and_transform_precision_may_differ']},null,2),{flag:'wx'});
console.log(JSON.stringify({output,covered_pixels:covered}));
GPU.device.destroy(); process.exit(0);
