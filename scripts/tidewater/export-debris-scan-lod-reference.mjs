// Execute unchanged scanned update/_put methods with observed batch outputs.
// Usage: SOURCE SCANNED_GEOMETRY NEW_OUTPUT
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {pathToFileURL} from 'node:url';
const [sourceArg,geometryArg,outputArg]=process.argv.slice(2);
if(!outputArg) throw Error('SOURCE GEOMETRY NEW_OUTPUT required');
const source=path.resolve(sourceArg),geometry=path.resolve(geometryArg),output=path.resolve(outputArg);
const sha=raw=>crypto.createHash('sha256').update(raw).digest('hex');
const git=(...args)=>execFileSync('git',['-c',`safe.directory=${source.replaceAll('\\','/')}`,'-C',source,...args],{encoding:'utf8'}).trim();
const commit='1438b1abfcaee3267092b75573014f4d9b4a983c';
if(git('rev-parse','HEAD')!==commit||git('status','--porcelain','--untracked-files=all')||fs.existsSync(output)) throw Error('Clean source/fresh output required');
for(const root of [source,geometry]){const rel=path.relative(root,output);if(!rel||(!rel.startsWith('..')&&!path.isAbsolute(rel))) throw Error('External output required');}
const raw=fs.readFileSync(path.join(geometry,'scanned-geometry.json'));
if(sha(raw)!=='666bbe16969d9289aee14ca721d380d9772dfec59335e933901853971987875d') throw Error('Acquired geometry differs');
const geo=JSON.parse(raw);
const at=name=>import(pathToFileURL(path.join(source,name)).href);
await at('test/headless.mjs');
const [{ScannedDebris},{Vector3,Quaternion,Matrix4,Frustum,Sphere,OrthographicCamera}]=await Promise.all([at('src/world/debris/ScannedDebris.js'),at('src/engine/index.js')]);
const cases=[];
for(const instance of geo.instances){
  const r={...instance.source,matrix:new Matrix4().fromArray(instance.matrix_column_major),radius:instance.radius_m};
  const edges=[22*(1-.12/2),22,22*(1+.12/2),70*(1-.12/2),70,70*(1+.12/2),260*(1-.12),260];
  const distances=[0,1,...edges.flatMap(edge=>[edge-1e-7,edge,edge+1e-7])];
  for(const effective of distances) for(const away of [false,true]){
    const requested=Math.max(0,effective+r.radius*6);
    const camera=new OrthographicCamera(-1024,1024,1024,-1024,.01,10000);
    camera.position.set(r.x,r.y,r.z+requested);
    camera.lookAt(r.x,r.y,away?camera.position.z+1:r.z-.01);
    camera.updateMatrixWorld();
    const matrices=[[],[],[]];
    const scanned=Object.create(ScannedDebris.prototype);
    Object.assign(scanned,{ready:true,instances:[r],_lastCam:new Vector3(1e9,0,0),_lastQuat:new Quaternion(),_frustum:new Frustum(),_m4:new Matrix4(),_sphere:new Sphere(),_v:new Vector3()});
    // Record only the source output sinks; source update and _put run unchanged.
    scanned.levels=[0,1,2].map(lod=>({iData:{array:new Float32Array(4),clearUpdateRanges(){},addUpdateRange(){}},mesh:{count:0,setMatrixAt(index,matrix){matrices[lod][index]=matrix.toArray();},instanceMatrix:{clearUpdateRanges(){},addUpdateRange(){}}}}));
    scanned.update(camera);
    const draws=[];
    for(const [lod,level] of scanned.levels.entries()) if(level.mesh.count){
      if(level.mesh.count!==1||matrices[lod].length!==1) throw Error('Unexpected source bucket');
      draws.push({lod,iData_f32:Array.from(level.iData.array),matrix_column_major:matrices[lod][0],casts_shadow:lod===0});
    }
    cases.push({source_id:instance.source_id,radius_m:r.radius,camera_position:camera.position.toArray(),away,effective_distance:Math.hypot(r.x-camera.position.x,r.y-camera.position.y,r.z-camera.position.z)-r.radius*6,draws});
  }
}
if(git('status','--porcelain','--untracked-files=all')) throw Error('Source changed');
const source_inputs=Object.fromEntries(['src/world/debris/ScannedDebris.js','src/materials/LODFade.js'].map(name=>[name,sha(fs.readFileSync(path.join(source,name)))]));
if(source_inputs['src/world/debris/ScannedDebris.js']!==geo.source_inputs.scanned_contract) throw Error('Source geometry contract differs');
const descriptor={schema:'forge.tidewater.debris-scanned-lod-reference',version:1,source_commit:commit,geometry_sha256:sha(raw),source_inputs,acquisition_sha256:sha(fs.readFileSync(import.meta.filename)),cases,
  limits:['Exact source CPU update/_put with observed batch sinks; no GPU mesh or draw.','Cases independently reset camera cache and isolate each acquired instance; movement-cache reuse and batch population performance remain open.','Native/WASM selection, editable transforms and shadow routing are not qualified.']};
const bytes=Buffer.from(JSON.stringify(descriptor,null,2));fs.mkdirSync(output);fs.writeFileSync(path.join(output,'reference.json'),bytes,{flag:'wx'});
console.log(JSON.stringify({status:'PASS',cases:cases.length,bytes:bytes.length,reference_sha256:sha(bytes),source_unchanged:true}));
