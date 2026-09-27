// Independent source oracle: fitting, temporal scheduling, disabling, invalidation.
import {readFileSync,writeFileSync,existsSync} from 'node:fs';
import {execFileSync} from 'node:child_process';
import {resolve,join} from 'node:path';
import {pathToFileURL} from 'node:url';
import {createHash} from 'node:crypto';
const [sourceArg,fixtureArg,outArg]=process.argv.slice(2);
if(!outArg) throw new Error('SOURCE FIXTURE NEW_JSON required');
const source=resolve(sourceArg),out=resolve(outArg),fixtureBytes=readFileSync(join(resolve(fixtureArg),'fixture.json'));
const fixture=JSON.parse(fixtureBytes),pin='1438b1abfcaee3267092b75573014f4d9b4a983c';
const git=(...a)=>execFileSync('git',['-C',source,...a],{encoding:'utf8'}).trim();
if(existsSync(out)||git('rev-parse','HEAD')!==pin||git('status','--porcelain')||fixture.source.commit!==pin) throw new Error('Clean source pin/new output required');
const moduleAt=p=>import(pathToFileURL(join(source,p)).href);
const {PerspectiveCamera,Vector3}=await moduleAt('src/engine/index.js');
const {SunShadows}=await moduleAt('src/engine/render/Shadows.js');
const {ShadowUniforms}=await moduleAt('src/engine/render/wgsl/lighting.js');
const c=fixture.camera_pose;
const camera=new PerspectiveCamera(c.fov_degrees,1920/1080,.06,60000);
camera.position.set(...c.position.map(Math.fround));camera.quaternion.set(...c.rotation.map(Math.fround));
const shadows=new SunShadows(),sun=new Vector3(-.886125497221313,.45498452073767237,.08815151197450805).normalize();
const cases=[];
const record=(name,invalidate=false)=>{
 if(invalidate) for(const cascade of shadows.cascades) cascade.dirty=true;
 camera.updateMatrixWorld();
 const updated=shadows.update(camera,sun),u=ShadowUniforms.fields;
 cases.push({name,enabled:shadows.enabled,invalidate,
  view:{world_from_view:camera.matrixWorld.elements.slice(),near:camera.near,fov_degrees:camera.fov,aspect:camera.aspect},
  sun:sun.toArray(),updated,sampling_enabled:u.enabled.value===1,
  cascades:shadows.cascades.map((v,i)=>({world:v.camera.matrixWorld.elements.slice(),view:v.camera.matrixWorldInverse.elements.slice(),
   projection:v.camera.projectionMatrix.elements.slice(),view_projection:v.viewProj.elements.slice(),radius:v.radius,near:v.camera.near,far:v.camera.far,
   info:u.cascades.value[i].toArray(),blend:u.blend.value[i].toArray()}))});
};
record('initial');
for(let i=0;i<12;i++) record('static-'+i);
for(let i=0;i<24;i++){camera.position.x+=.001*(i+1);camera.position.z-=.031;record('moving-'+i);}
shadows.enabled=false;
for(let i=0;i<3;i++){camera.position.y+=1;record('disabled-'+i);}
shadows.enabled=true;record('re-enabled');record('caster-edit',true);
for(const y of [.9899,.9901,1,-1,-.05001,-.04999,.5]){
 sun.set(Math.sqrt(1-y*y),y,0);record('sun-y-'+y);
 for(let i=0;i<4;i++)record('sun-y-'+y+'-static-'+i);
}
for(let i=0;i<12;i++){
 camera.position.set(-512+i*47,5+i*13,-301+i*31);camera.lookAt(new Vector3(3,12,-17));
 camera.fov=35+i*5;camera.aspect=i%2?9/16:16/9;record('rotated-'+i);
}
if(git('status','--porcelain')) throw new Error('Source changed');
writeFileSync(out,JSON.stringify({schema:'forge.tidewater.sun-cascade-oracle',version:1,source_commit:pin,
 fixture_sha256:createHash('sha256').update(fixtureBytes).digest('hex'),
 config:{size:shadows.size,splits:shadows.splits,light_margin:shadows.lightMargin,normal_bias:shadows.normalBias},cases},null,2),{flag:'wx'});
console.log(JSON.stringify({out,cases:cases.length}));
