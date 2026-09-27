// Original grass geometry, density, occupancy and camera-selection oracle.
import {readFileSync,writeFileSync,mkdirSync,existsSync} from 'node:fs';
import {resolve,join} from 'node:path';
import {pathToFileURL} from 'node:url';
import {execFileSync} from 'node:child_process';
import {createHash} from 'node:crypto';
const [sourceArg,fixtureArg,outArg]=process.argv.slice(2);
if(!outArg) throw new Error('SOURCE FIXTURE NEW_DIRECTORY required');
const source=resolve(sourceArg),out=resolve(outArg),fixture=JSON.parse(readFileSync(join(resolve(fixtureArg),'fixture.json')));
const pin='1438b1abfcaee3267092b75573014f4d9b4a983c';
const git=(...a)=>execFileSync('git',['-C',source,...a],{encoding:'utf8'}).trim();
if(git('rev-parse','HEAD')!==pin||git('status','--porcelain')||fixture.source.commit!==pin||existsSync(out)) throw new Error('Clean pin and new output required');
const moduleAt=p=>import(pathToFileURL(join(source,p)).href),hash=b=>createHash('sha256').update(b).digest('hex');
await moduleAt('test/headless.mjs');
const {GPU}=await moduleAt('src/engine/gpu/GPU.js');await GPU.init({headless:true});
const {TerrainData}=await moduleAt('src/world/TerrainData.js');
const {Village}=await moduleAt('src/world/Village.js');
const {Vegetation}=await moduleAt('src/world/Vegetation.js');
const {Colliders}=await moduleAt('src/world/Colliders.js');
const {Scene,PerspectiveCamera}=await moduleAt('src/engine/index.js');
const {collectModules}=await moduleAt('src/engine/gpu/Shader.js');
const terrain=new TerrainData(),scene=new Scene(),village=new Village({scene,terrain,colliders:new Colliders()});
const vegetation=new Vegetation({scene,terrain,village}),grass=vegetation.grass;
if(hash(Buffer.from(terrain.heights.buffer))!==fixture.files['heights.bin']) throw new Error('Terrain differs');
const c=fixture.camera_pose,camera=new PerspectiveCamera(c.fov_degrees,1920/1080,.06,60000);
camera.position.set(...c.position.map(Math.fround));camera.quaternion.set(...c.rotation.map(Math.fround));camera.updateMatrixWorld();
grass.update(camera);
mkdirSync(out,{recursive:true});const files={};
const save=(name,data)=>{const bytes=typeof data==='string'?Buffer.from(data):Buffer.from(data.buffer,data.byteOffset,data.byteLength);writeFileSync(join(out,name),bytes,{flag:'wx'});files[name]=hash(bytes);return name;};
const levels=grass.levels.map((level,index)=>{
 const attributes={};
 for(const [name,attr] of Object.entries(level.geometry.attributes)) {
  if(name!=='iCell') attributes[name]=save(`grass-${index}-${name}.bin`,attr.array);
 }
 return {attributes,indices:save(`grass-${index}-indices.bin`,new Uint32Array(level.geometry.index.array)),capacity:level.max,
   oracle_cells:Array.from({length:level.count},(_,i)=>Array.from(level.arr.subarray(i*4,i*4+4)))};
});
const material=grass.material;
const modules=collectModules(material.modules).map(m=>({name:m.name,code:save(`grass-module-${m.name}.wgsl`,m.code)}));
const mask=grass.maskTex.pendingData;
if(!(mask instanceof Uint8Array)) throw new Error('Original grass mask no longer pending');
const meta={schema:'forge.tidewater.grass-probe',version:1,source_commit:pin,camera:c,
 height_sha256:fixture.files['heights.bin'],cells_per_side:grass.cellsPerSide,
 flags:save('grass-flags.bin',grass.cellFlags),min_y:save('grass-min-y.bin',grass.cellMinY),max_y:save('grass-max-y.bin',grass.cellMaxY),
 mask:{width:grass.maskTex.width,height:grass.maskTex.height,file:save('grass-mask.bin',mask)},levels,
 material:{vertex:material.vertex,surface:material.surface,modules},files};
if(git('status','--porcelain')) throw new Error('Source changed');
writeFileSync(join(out,'grass-source.json'),JSON.stringify(meta,null,2),{flag:'wx'});
console.log(JSON.stringify({out,cells:levels.map(l=>l.oracle_cells.length),vertices:grass.levels.map(l=>l.geometry.attributes.position.count),mask:meta.mask}));
GPU.device.destroy();process.exit(0);
