// Original six vegetation groups, fixed camera, geometry/alpha oracle only.
import {readFileSync,writeFileSync,existsSync} from 'node:fs';
import {execFileSync} from 'node:child_process';
import {resolve,join} from 'node:path';
import {pathToFileURL} from 'node:url';
import {createHash} from 'node:crypto';
import {captureObjectShadows} from './capture-object-shadows.mjs';
import {terrainWetnessHook} from './shore-reference-hooks.mjs';
const [sourceArg,fixtureArg,outputArg,mode,skyOption,waterOption,debugOption]=process.argv.slice(2);
if(process.argv.length>9 || (debugOption&&!/^(--water-debug=(2|3|11)|--terrain-debug=wetness)$/.test(debugOption)))throw Error('Invalid diagnostic option');
const waterDebug=debugOption?.startsWith('--water-debug=')?Number(debugOption.split('=')[1]):0;
const terrainDebug=debugOption==='--terrain-debug=wetness';
if(waterOption && !['--water-geometry','--water-scene-color','--water-scene-depth','--water-optics','--water-shore','--water-shore-sim','--water-detail','--water-breakers','--water-spray'].includes(waterOption))throw new Error('Unknown water option');
const waterSpray=waterOption==='--water-spray';
const waterBreakers=waterSpray||waterOption==='--water-breakers';
const waterDetail=waterBreakers||waterOption==='--water-detail';
const waterSim=waterDetail||waterOption==='--water-shore-sim';
const waterShore=waterSim||waterOption==='--water-shore';
const waterOptics=waterShore||waterOption==='--water-optics';
if(waterDebug&&!waterOptics)throw Error('Water debug requires optical material');
if(terrainDebug&&!waterSim)throw Error('Terrain wetness diagnostic requires ShoreSim');
const waterGeometry=!!waterOption;
const waterDiagnostic=waterOption==='--water-scene-color'?1:waterOption==='--water-scene-depth'?2:0;
const cloudSky=skyOption?.startsWith('--cloud-sky=')?resolve(skyOption.slice('--cloud-sky='.length)):null;
if(skyOption && skyOption!=='--clear-sky' && !cloudSky)throw new Error('Unknown sky option');
const clearSky=skyOption==='--clear-sky'||Boolean(cloudSky);
if(mode && mode!=='--grass' && mode!=='--direct' && !mode.startsWith('--environment=') && !mode.startsWith('--scene=') && !mode.startsWith('--shadows=') && !mode.startsWith('--village-shadows=') && !mode.startsWith('--terrain-village-shadows=')) throw new Error('Unknown oracle mode');
const grassOnly=mode==='--grass';
const direct=mode==='--direct';
const villageShadows=mode?.startsWith('--village-shadows=')||false;
const terrainVillageShadows=mode?.startsWith('--terrain-village-shadows=')||false;
const objectShadows=terrainVillageShadows||villageShadows||mode?.startsWith('--shadows=')||false;
const fullScene=objectShadows||mode?.startsWith('--scene=')||false;
const environmentPath=terrainVillageShadows?resolve(mode.slice('--terrain-village-shadows='.length)):villageShadows?resolve(mode.slice('--village-shadows='.length)):objectShadows?resolve(mode.slice(10)):fullScene?resolve(mode.slice(8)):mode?.startsWith('--environment=')?resolve(mode.slice(14)):null;
if(!sourceArg||!fixtureArg||!outputArg) throw new Error('SOURCE FIXTURE NEW_PNG required');
const source=resolve(sourceArg),fixturePath=resolve(fixtureArg),output=resolve(outputArg);
const PIN='1438b1abfcaee3267092b75573014f4d9b4a983c';
const git=(...args)=>execFileSync('git',['-C',source,...args],{encoding:'utf8'}).trim();
if(git('rev-parse','HEAD')!==PIN||git('status','--porcelain')) throw new Error('Clean pinned source required');
if(existsSync(output)||existsSync(output+'.json')) throw new Error('Refusing overwrite');
const bytes=readFileSync(join(fixturePath,'fixture.json')),fixture=JSON.parse(bytes);
if(fixture.source.commit!==PIN||!fixture.camera_pose) throw new Error('Pinned camera required');
const moduleAt=p=>import(pathToFileURL(join(source,p)).href);
const {writePNG}=await moduleAt('test/headless.mjs');
const {GPU}=await moduleAt('src/engine/gpu/GPU.js');
await GPU.init({headless:true});GPU.device.pushErrorScope('validation');
const E=await moduleAt('src/engine/index.js');
const {RenderTarget}=await moduleAt('src/engine/gpu/Texture.js');
const {readTexture,readBuffer}=await moduleAt('src/engine/gpu/Readback.js');
const {MeshRenderer}=await moduleAt('src/engine/render/MeshRenderer.js');
const {SunShadows}=await moduleAt('src/engine/render/Shadows.js');
const {G,FrameUniforms,setFrameCamera}=await moduleAt('src/engine/render/Frame.js');
const {TerrainData}=await moduleAt('src/world/TerrainData.js');
const {Village}=await moduleAt('src/world/Village.js');
const {Colliders}=await moduleAt('src/world/Colliders.js');
const {Vegetation}=await moduleAt('src/world/Vegetation.js');
const {vegParams}=await moduleAt('src/world/vegetation/VegNodes.js');
const {ShaderModule}=await moduleAt('src/engine/gpu/Shader.js');
const {SceneLighting}=await moduleAt('src/engine/render/wgsl/lighting.js');
const constructionScene=new E.Scene(),terrain=new TerrainData();
const village=new Village({scene:constructionScene,terrain,colliders:new Colliders()});
const vegetation=new Vegetation({scene:constructionScene,terrain,village});
const W=1920,H=1080,c=fixture.camera_pose;
const camera=new E.PerspectiveCamera(c.fov_degrees,W/H,.06,60000);
camera.position.set(...c.position.map(Math.fround));camera.quaternion.set(...c.rotation.map(Math.fround));camera.updateMatrixWorld();
vegParams.fields.camPos.value.copy(camera.position);vegParams.fields.gustOffset.value.set(0,0);
G.time.value=2;G.dt.value=0;G.night.value=0;G.windSpeed.value=6;G.windDir.value.set(1,0);
G.sunDir.value.set(0.3,0.6,-0.7).normalize();
G.sunColor.value.setRGB(1,1,1);
FrameUniforms.fields.frameIndex.value=124;setFrameCamera(camera,W,H);
const renderer=new MeshRenderer(),shadows=new SunShadows();
vegetation.leafAtlas.bake();vegetation.atlas.bake();
const scene=new E.Scene(),counts=[];
for(const type of vegetation.types){
  type.update(camera.position,true);
  for(const mesh of type.meshes){if(!objectShadows||villageShadows||terrainVillageShadows) mesh.castShadow=false;if(!grassOnly) scene.add(mesh);}
  counts.push({name:type.name,near:type.near.count,far:type.far?.count||0,
    near_ids:Array.from(type.nearIds.subarray(0,type.near.count))});
}
let hillGPU=null,backgroundSky=null,terrainView=null,receiverClouds=null;
if(grassOnly || (fullScene && fixture.grass_probe)){
  vegetation.grass.update(camera);
  for(const mesh of vegetation.grass.meshes){mesh.castShadow=false;scene.add(mesh);}
  if(grassOnly){vegetation.grass.material.lit=false;vegetation.grass.material.output='r.color = vec4f(1.0);';}
  counts.push({name:'grass',levels:vegetation.grass.levels.map(l=>l.count)});
}
if(fullScene){
  const {TerrainGPU}=await moduleAt('src/world/TerrainGPU.js');
  const {Terrain}=await moduleAt('src/world/Terrain.js');
  const heights=Buffer.from(terrain.heights.buffer,terrain.heights.byteOffset,terrain.heights.byteLength);
  if(createHash('sha256').update(heights).digest('hex')!==fixture.files['heights.bin']) throw new Error('Terrain differs');
  hillGPU=new TerrainGPU(terrain);
  if(waterShore){
    const {computeShoreField}=await moduleAt('src/world/ShoreField.js');
    const {WORLD}=await moduleAt('src/world/WorldLayout.js');
    const field=computeShoreField(terrain,{res:512,swellDir:[WORLD.swellDir.x,WORLD.swellDir.y]});
    hillGPU.setShoreField(field);
  }
  terrainView=new Terrain({scene,terrainData:terrain,terrainGPU:hillGPU,sunShadow:false});
  terrainView.update(camera);if(!objectShadows||villageShadows) terrainView.mesh.castShadow=false;
  for(const child of [...village.group.children]) if(child.name==='village_fish') village.group.remove(child);
  const unshadow=node=>{node.castShadow=false;for(const child of node.children||[]) unshadow(child);};
  if(!objectShadows) unshadow(village.group);scene.add(village.group);
}
if(environmentPath){
  const env=JSON.parse(readFileSync(join(environmentPath,'environment.json')));
  if(env.source_commit!==PIN||env.schema!=='forge.tidewater.environment-probe') throw new Error('Pinned environment required');
  for(const [name,hash] of Object.entries(env.files)){
    if(name.includes('/')||name.includes('\\')||createHash('sha256').update(readFileSync(join(environmentPath,name))).digest('hex')!==hash) throw new Error('Environment hash mismatch');
  }
  const {Atmosphere}=await moduleAt('src/sky/Atmosphere.js');
  const {Sky}=await moduleAt('src/sky/Sky.js');
  const {Environment}=await moduleAt('src/sky/Environment.js');
  const {Texture,StorageBuffer}=await moduleAt('src/engine/gpu/Texture.js');
  // Install the original Environment hooks, then replace only their input data.
  const atmosphere=new Atmosphere(),sky=new Sky(atmosphere);
  if(cloudSky){
    const cloudMeta=JSON.parse(readFileSync(join(cloudSky,'environment.json')));
    const pose=fixture.camera_pose;
    if(cloudMeta.source_commit!==PIN||JSON.stringify(cloudMeta.sun_direction)!==JSON.stringify(env.sun_direction)
      ||JSON.stringify(['x','y','z'].map(k=>cloudMeta.camera.position[k]))!==JSON.stringify(pose.position)
      ||JSON.stringify(['_x','_y','_z','_w'].map(k=>cloudMeta.camera.quaternion[k]))!==JSON.stringify(pose.rotation)
      ||cloudMeta.camera.fov!==pose.fov_degrees)throw Error('Cloud reference mismatch');
    const data=name=>{const bytes=readFileSync(join(cloudSky,name));if(createHash('sha256').update(bytes).digest('hex')!==cloudMeta.files[name])throw Error('Cloud file hash mismatch');return bytes;};
    const {SkyProClouds}=await moduleAt('src/sky/SkyProClouds.js');
    const originalFetch=globalThis.fetch;
    globalThis.fetch=async url=>{if(!['/clouds/baseShape64.bin','/clouds/blueNoise.bin'].includes(url))throw Error('Unexpected cloud fetch');return new Response(readFileSync(join(source,'public',url.slice(1))));};
    const clouds=new SkyProClouds(null,atmosphere);
    try{await clouds.ready;}finally{globalThis.fetch=originalFetch;}
    for(const [texture,w,h,file] of [[clouds.panorama,512,160,'cloud-step120-panorama.bin'],[clouds.colors[1],960,540,'cloud-step120-color1.bin']]){
      texture.usageList=[...texture.usageList,'copyDst'];texture.resize(w,h);texture.upload(new Uint8Array(data(file)));
    }
    clouds.viewTex=clouds.colors[1];
    const bytes=data('cloud-step120-public.bin');const f=new Float32Array(bytes.buffer.slice(bytes.byteOffset,bytes.byteOffset+bytes.byteLength));
    clouds.viewCam.right.value.set(...f.slice(0,3));clouds.viewCam.up.value.set(...f.slice(4,7));clouds.viewCam.fwd.value.set(...f.slice(8,11));
    clouds.viewCam.tan.value.set(...f.slice(12,14));clouds.viewValid.value=f[14];
    if(fixture.cloud_shadow_probe){
      clouds.shadowMap.usageList=[...clouds.shadowMap.usageList,'copyDst'];
      clouds.shadowMap.resize(256,256);clouds.shadowMap.upload(new Uint8Array(data('cloud-step120-shadow.bin')));
      clouds.shadowCenter.value.set(f[16],f[17]);clouds.shadowSize.value=f[18];clouds.shadowStrength.value=f[19];
      receiverClouds=clouds;
    }
    sky.clouds=clouds;
  }
  const skyEnv=new Environment(null,null,sky);
  if(clearSky){
    if(env.night!==0||!fullScene)throw new Error('Clear sky oracle currently requires daytime full scene');
    atmosphere.sunDir.value.set(...env.sun_direction);
    atmosphere.update(1/60,camera.position.y);
    backgroundSky=sky;
  }
  const cube=new Texture({width:128,height:128,dimension:'cube',format:'rgba16float',mips:6,usage:['sample','copyDst']});
  cube.getGPU();
  for(const mip of env.mips) for(let face=0;face<6;face++)
    cube.upload(new Uint8Array(readFileSync(join(environmentPath,mip.faces[face]))),{mip:mip.level,layer:face,layers:1});
  skyEnv.targets[0]=cube;
  const sh=readFileSync(join(environmentPath,env.sh));
  skyEnv.sh[0]=new StorageBuffer({count:9,type:'vec4f',data:new Float32Array(sh.buffer.slice(sh.byteOffset,sh.byteOffset+sh.byteLength))});
  G.sunDir.value.set(...env.sun_direction);G.sunColor.value.setRGB(...env.sun_color);
  G.envIntensity.value=env.environment_intensity;G.night.value=env.night;
}else if(direct){
  for(const name of ['envDiffuse','envSpecular']) {
    const signature=name==='envDiffuse'?'N:vec3f':'R:vec3f,roughness:f32';
    SceneLighting.set(name,new ShaderModule({name:'direct-probe-'+name,
      code:`fn hook${name[0].toUpperCase()+name.slice(1)}(${signature})->vec3f{return vec3f(0.0);}`}));
  }
}else{
  for(const material of vegetation.materials){material.lit=false;material.output='r.color = vec4f(1.0);';}
}
if(fixture.hill_shadow_probe && !grassOnly){
  if(!hillGPU) throw new Error('Heightfield shadow oracle requires full scene mode');
  hillGPU.updateSunShadow(null,true);
  SceneLighting.set('directModulation',new ShaderModule({name:'hill-shadow-oracle',deps:[hillGPU.module,...(receiverClouds?[receiverClouds.shadowModule]:[])],
    code:`fn hookDirectModulation(P:vec3f,N:vec3f)->vec3f{return vec3f(terrainSunShadowAt(P)${receiverClouds?'*cloudsShadow(P.xz)':''});}`}));
}
const spraySchedule=[];
let waterNodes=null,waterScene=null,waterCopy=null,waterMaterial=null,breakers=null,breakerScene=null,spray=null,sprayScene=null;
if(waterGeometry){
  if(!fixture.ocean_geometry_probe||!hillGPU||!clearSky)throw new Error('Prepared water geometry scene required');
  const {OceanFFT}=await moduleAt('src/ocean/OceanFFT.js');
  const {CDLOD}=await moduleAt('src/core/CDLOD.js');
  const {WaterSurface}=await moduleAt('src/ocean/WaterSurface.js');
  const {Material}=await moduleAt('src/engine/render/Material.js');
  const fft=new OceanFFT(null),lod=new CDLOD({gridSize:32,leafSize:8,levels:12,minY:-25,maxY:25});
  const {createFoamTexture}=await moduleAt('src/ocean/FoamTexture.js');
  const surface=new WaterSurface({fft,cdlod:lod,foamTexture:waterOptics?createFoamTexture(null):null});surface.terrain=hillGPU;
  if(waterDetail){const {SeaDetail}=await moduleAt('src/ocean/SeaDetail.js');surface.detail=new SeaDetail();}
  if(waterShore){const {ShoreWaves}=await moduleAt('src/ocean/ShoreWaves.js');surface.shore=new ShoreWaves(hillGPU);}
  if(waterSim){
    const {ShoreSim}=await moduleAt('src/ocean/ShoreSim.js');
    const {SurfFoam}=await moduleAt('src/ocean/SurfFoam.js');
    surface.shoreSim=new ShoreSim(null,{terrainGPU:hillGPU,shore:surface.shore});
    const surfFoam=new SurfFoam({shoreSim:surface.shoreSim});surface.foamShading=args=>surfFoam.shading(args);
    terrainView.wetness={modules:[surface.shoreSim.module],code:terrainWetnessHook(source)};
    terrainView.finalizeMaterial();
    if(terrainDebug){terrainView.material.lit=false;terrainView.material.output='r.color=vec4f(terrainWetness(in.P.xz,in.P.y),0.0,1.0);';}
  }
  waterCopy=new RenderTarget(W,H,{colors:['rgba8unorm-srgb'],depth:'depth32float',label:'water opaque copy'});
  if(waterSpray){
    const {WaterQuery}=await moduleAt('src/ocean/WaterQuery.js');
    const {Spray}=await moduleAt('src/fx/Spray.js');
    spray=new Spray(null,{query:new WaterQuery(null,surface),terrain:hillGPU,sceneCopy:waterCopy,clouds:receiverClouds});
    sprayScene=new E.Scene();sprayScene.add(spray.mesh);
  }
  if(waterBreakers){const {Breakers}=await moduleAt('src/ocean/Breakers.js');breakers=new Breakers(null,{surface,shore:surface.shore,terrainData:terrain,sky:backgroundSky,spray,clouds:receiverClouds});breakerScene=new E.Scene();breakerScene.add(breakers.mesh);}
  const code=surface.module.code.split('struct WaterSurfaceFrag')[0];
  const vertexModule=new ShaderModule({name:'waterGeometry',deps:[fft.module,lod.module,surface.attenuationModule,hillGPU.module],
    uniforms:surface.params,uniformName:'waterSurface',code});

  const {WaterMaterial}=await moduleAt('src/ocean/WaterMaterial.js');
  const material=waterOptics?new WaterMaterial({surface,sky:backgroundSky,sceneCopy:waterCopy}):new Material({name:'water geometry oracle',lit:false,side:'double',blending:'none',depthWrite:true,
    modules:[vertexModule],attributes:{nodeData:'vec4f'},
    bindings:{waterSceneColor:{texture:waterCopy.texture},waterSceneDepth:{texture:waterCopy.depthTexture,sampleType:'unfilterable-float'}},
    vertex:'let s=waterSurfaceVertex(v.nodeData,v.position.xz);v.useWorld=true;v.worldPos=s.position;v.worldNormal=vec3f(0.0,1.0,0.0);',
    output:waterDiagnostic===1?'r.color=textureSampleLevel(waterSceneColor,smpLinearClamp,in.pixel*frame.invResolution,0.0);'
      :waterDiagnostic===2?'r.color=vec4f(vec3f(textureLoad(waterSceneDepth,vec2i(in.pixel),0).x*100.0),1.0);':'r.color=vec4f(1.0);'});
  material.lightingHooks=false;
  if(waterOptics){waterMaterial=material;material.clouds=receiverClouds;material.debugMode.value=waterDebug;}
  const mesh=new E.Mesh(lod.geometry,material);mesh.castShadow=false;mesh.frustumCulled=false;
  lod.update(camera);waterNodes=Array.from({length:lod.count},(_,i)=>Array.from(lod.nodeArray.slice(i*4,i*4+4)));
  waterScene=new E.Scene();waterScene.add(mesh);
  GPU.submit();await GPU.queue.onSubmittedWorkDone();
  let simTime=0;
  for(let step=1;step<=120;step++){
    GPU.beginFrame();G.dt.value=1/60;fft.update(1/60);
    if(waterDetail)surface.detail.update(1/60);
    if(waterSim){simTime+=1/60;G.time.value=simTime;FrameUniforms.fields.frameIndex.value=step;surface.shoreSim.update();}
    const emitterBudget=spray?breakers.uniforms.fields.budget.value:null;
    if(breakers)breakers.update(camera);
    if(spray){spray.update();spraySchedule.push({frame:step,emitter_budget:emitterBudget,effective_gpu_seed:spray.frameSeed.value});}
    GPU.submit();await GPU.queue.onSubmittedWorkDone();
  }
  if(spray)writeFileSync(output+'.spray-schedule.json',JSON.stringify(spraySchedule),{flag:'wx'});
  if(spray)for(const [name,buffer,size] of [['pos',spray.pos,spray.N*16],['vel',spray.vel,spray.N*16],['info',spray.info,spray.N*16],['head',spray.head,4]])writeFileSync(output+'.spray-'+name+'.bin',Buffer.from(await readBuffer(buffer,size)),{flag:'wx'});
  if(breakers)writeFileSync(output+'.crests.f32.bin',Buffer.from(await readBuffer(breakers.crest,breakers.NS*6*16)),{flag:'wx'});
  if(waterSim)writeFileSync(output+'.shore-state.rgba16.bin',Buffer.from((await readTexture(surface.shoreSim.stateA)).data),{flag:'wx'});
  if(waterDetail){surface.detail.uniforms._pack();writeFileSync(output+'.sea-detail.bin',Buffer.from(new Uint8Array(surface.detail.uniforms.data)),{flag:'wx'});}
  G.dt.value=0;G.time.value=2;FrameUniforms.fields.frameIndex.value=124;setFrameCamera(camera,W,H);
}
const format=direct||environmentPath?'rgba8unorm-srgb':'rgba8unorm';
let skyPass=null;
if(clearSky){
  if(!backgroundSky)throw new Error('Clear sky requires environment');
  const {FullscreenPass}=await moduleAt('src/engine/render/FullscreenPass.js');
  skyPass=new FullscreenPass({label:'clear sky oracle',modules:[backgroundSky.module],colorFormats:[format],
    depthFormat:'depth32float',depthCompare:'equal',depthWrite:false,depth:0,
    code:'fn fragment(in:FSIn)->vec4f{return vec4f(skyViewRadiance(viewRay(in.pos.xy*frame.invResolution)),1.0);}'});
}
const target=new RenderTarget(W,H,{colors:[format],depth:'depth32float',label:'vegetation-oracle'});
shadows.render(scene,renderer,shadows.update(camera,G.sunDir.value));setFrameCamera(camera,W,H);
renderer.render(scene,{camera,kind:'color',colorViews:[target.texture.view()],colorFormats:[format],
  clearColors:[[0,0,0,1]],depthView:target.depthTexture.view(),depthFormat:'depth32float',clearDepth:0,
  after:skyPass?pass=>skyPass.draw(pass):undefined});
if(waterScene){
  const enc=GPU.getEncoder();
  enc.copyTextureToTexture({texture:target.texture.getGPU()},{texture:waterCopy.texture.getGPU()},[W,H,1]);
  enc.copyTextureToTexture({texture:target.depthTexture.getGPU(),aspect:'depth-only'},
    {texture:waterCopy.depthTexture.getGPU(),aspect:'depth-only'},[W,H,1]);
  renderer.render(waterScene,{camera,kind:'color',colorViews:[target.texture.view()],colorFormats:[format],
    depthView:target.depthTexture.view(),depthFormat:'depth32float'});
}
if(breakerScene)renderer.render(breakerScene,{camera,kind:'color',colorViews:[target.texture.view()],colorFormats:[format],depthView:target.depthTexture.view(),depthFormat:'depth32float'});
if(sprayScene)renderer.render(sprayScene,{camera,kind:'color',colorViews:[target.texture.view()],colorFormats:[format],depthView:target.depthTexture.view(),depthFormat:'depth32float'});
GPU.submit();await GPU.queue.onSubmittedWorkDone();
const result=await readTexture(target.texture),rgba=new Uint8Array(result.data);
if(waterOptics){
  FrameUniforms._pack();
  writeFileSync(output+'.frame.bin',Buffer.from(new Uint8Array(FrameUniforms.data)),{flag:'wx'});
  if(waterDebug){waterMaterial.uniformBlock._pack();writeFileSync(output+'.material.bin',Buffer.from(new Uint8Array(waterMaterial.uniformBlock.data)),{flag:'wx'});}
}
if(objectShadows) await captureObjectShadows(moduleAt,shadows,output,scene);
const error=await GPU.device.popErrorScope();if(error) throw new Error(error.message);
if(git('status','--porcelain')) throw new Error('Source changed');
writePNG(output,W,H,rgba);
writeFileSync(output+'.json',JSON.stringify({source_commit:PIN,
 fixture_sha256:createHash('sha256').update(bytes).digest('hex'),width:W,height:H,camera:c,
 renderer:'upstream_webgpu',purpose:grassOnly?'grass_coverage':fullScene?'terrain_village_vegetation_acquired_environment':environmentPath?'vegetation_acquired_environment':direct?'vegetation_unit_direct_light':'six_group_vegetation_coverage',time:2,frame:124,counts,
 environment:environmentPath,
 clear_sky:clearSky,
 cloud_shadow_receivers:Boolean(receiverClouds),
 cloud_sky:cloudSky?{acquisition:cloudSky,mode:'original_renderer_acquired_cloud_history'}:null,
 ...(waterDebug?{water_debug:{mode:waterDebug,material_sha256:createHash('sha256').update(readFileSync(output+'.material.bin')).digest('hex'),purpose:'original_builtin_diagnostic_not_optical_parity'}}:{}),
 ...(terrainDebug?{terrain_debug:{mode:'wetness',purpose:'paired_function_output_not_optical_parity'}}:{}),
 water_geometry:waterGeometry?{nodes:waterNodes,steps:120,sea:'default',surface_options:{...fixture.ocean_geometry_probe.surface_options,shore:waterShore,...(waterBreakers?{breakers:true}:{}),...(waterDetail?{detail:true}:{}),...(waterSim?{shore_sim:true,surf_foam:true,terrain_wetness:true}:{})},output:waterOptics?'original_water_optics':waterDiagnostic===1?'opaque_scene_color':waterDiagnostic===2?'opaque_scene_depth_times_100':'white_geometry_no_water_optics'}:null,
 ...(waterSpray?{spray:{steps:120,capacity:spray.N,gpu_capacity:spray.NG,cpu_emitters:false,body:false,global_wind:{speed:G.windSpeed.value,direction:[G.windDir.value.x,G.windDir.value.y]},render_order:20,blend:'premultiplied',depth_write:false,schedule_sha256:createHash('sha256').update(readFileSync(output+'.spray-schedule.json')).digest('hex')}}:{}),
 ...(waterBreakers?{breakers:{steps:120,spray:waterSpray,clouds:Boolean(receiverClouds),stations:breakers.NS,crests_sha256:createHash('sha256').update(readFileSync(output+'.crests.f32.bin')).digest('hex')}}:{}),
 ...(waterDetail?{sea_detail:{steps:120,dt:1/60,uniform_sha256:createHash('sha256').update(readFileSync(output+'.sea-detail.bin')).digest('hex')}}:{}),
 ...(waterSim?{shore_sim:{frame:120,dt:1/60,initial_state:'zero',deposit_schedule:waterSpray?'gpu_spray_previous_step':[],state_sha256:createHash('sha256').update(readFileSync(output+'.shore-state.rgba16.bin')).digest('hex'),terrain_wetness:true,surf_foam:true}}:{}),
 grass:grassOnly || !!(fullScene && fixture.grass_probe),
 heightfield_shadows:!!fixture.hill_shadow_probe && !grassOnly,
 object_shadows:objectShadows,
 object_shadow_scope:terrainVillageShadows?'terrain_village':villageShadows?'village_only':objectShadows?'terrain_village_near_vegetation':'none',
 lighting:direct||environmentPath?{sun_direction:G.sunDir.value,sun_color:G.sunColor.value,environment:!!environmentPath,cast_shadows:objectShadows}:null,
 limitations:grassOnly?['no_lighting_or_scene_occlusion','fixed_camera_and_time']
   :objectShadows?[receiverClouds?'no_ground_bounce':'no_cloud_shadows_or_bounce','cold_environment_not_baseline_replay','fixed_camera_and_time','no_fish_props']
   :fixture.hill_shadow_probe?[...(fixture.grass_probe?[]:['no_grass']),'no_object_or_cloud_shadows_or_bounce','cold_environment_not_baseline_replay','fixed_camera_and_time']
   :environmentPath?['no_grass','no_scene_occlusion_or_bounce','cold_environment_not_baseline_replay','fixed_camera_and_time']
   :direct?['no_grass','no_environment_or_scene_occlusion','unit_light_not_full_scene','fixed_camera_and_time']
   :['no_grass','no_lighting_or_scene_occlusion','fixed_camera_and_time']},null,2),{flag:'wx'});
console.log(JSON.stringify({output,counts:counts.map(({near_ids,...item})=>item)}));
GPU.device.destroy();process.exit(0);
