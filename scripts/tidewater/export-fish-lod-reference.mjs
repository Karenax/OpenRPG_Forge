// SPDX-License-Identifier: MIT
// Execute the intact source cull method; only the batch output is recorded.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {pathToFileURL} from 'node:url';
import {execFileSync} from 'node:child_process';
const [sourceArg,packageArg,outputArg]=process.argv.slice(2);
if(!sourceArg||!packageArg||!outputArg||process.versions.node!=='22.22.3')throw Error('Pinned Node, SOURCE PACKAGE NEW_OUTPUT required');
const source=path.resolve(sourceArg),root=path.resolve(packageArg),output=path.resolve(outputArg);
const sha=bytes=>crypto.createHash('sha256').update(bytes).digest('hex');
const git=(...args)=>execFileSync('git',['-C',source,...args],{encoding:'utf8'}).trim();
const commit='1438b1abfcaee3267092b75573014f4d9b4a983c';
if(git('rev-parse','HEAD')!==commit||git('status','--porcelain','--untracked-files=no'))throw Error('Intact reference required');
if(fs.existsSync(output))throw Error('Fresh output required');
const manifestBytes=fs.readFileSync(path.join(root,'package.json'));
if(sha(manifestBytes)!=='e578b44e93d18d79ec06c875b76dae005f71c17ecf1d0ff09aa3ecddd651330a')throw Error('Package pin differs');
const manifest=JSON.parse(manifestBytes);
const payload=name=>{
 const pin=manifest.files[name],bytes=fs.readFileSync(path.join(root,name));
 if(!pin||bytes.length!==pin.bytes||sha(bytes)!==pin.sha256)throw Error('Altered payload');return bytes;
};
for(const name of Object.keys(manifest.files))payload(name);
const props=JSON.parse(payload(manifest.props.file));
await import(pathToFileURL(path.join(source,'test/headless.mjs')).href);
const THREE=await import(pathToFileURL(path.join(source,'src/engine/index.js')).href);
const {FishProps}=await import(pathToFileURL(path.join(source,'src/world/fish/FishProps.js')).href);
const cases=[];
for(const [ordinal,instance] of props.instances.entries()){
 const position=new Float32Array(instance.record_f32.slice(0,4)),length=position[3],threshold=length*28;
 const distances=[0,1,10,21,22,23,81,85,90,91,threshold*0.88,threshold];
 for(const edge of [22,81,90,threshold*0.88,threshold])distances.push(edge-1e-7,edge+1e-7);
 for(const requested of [...new Set(distances)].filter(value=>value>=0))for(const away of [false,true]){
  const camera=new THREE.OrthographicCamera(-1024,1024,1024,-1024,0.01,10000);
  camera.position.set(position[0],position[1],position[2]+requested);
  camera.lookAt(position[0],position[1],away?camera.position.z+1:position[2]-0.01);
  camera.updateMatrixWorld();
  const matrix=new THREE.Matrix4().multiplyMatrices(camera.projectionMatrix,camera.matrixWorldInverse);
  const frustum=new THREE.Frustum().setFromProjectionMatrix(matrix,camera.coordinateSystem,camera.reversedDepth);
  const sphere=new THREE.Sphere(new THREE.Vector3(...position.slice(0,3)),length*0.8);
  const visible=frustum.intersectsSphere(sphere),draws=[];
  const batch={begin(){},add(kind,index){draws.push({lod:kind-instance.kind0,index,fade:1,outgoing:false});},addFade(kind,index,fade,outgoing){draws.push({lod:kind-instance.kind0,index,fade,outgoing});},commit(){}};
  FishProps.prototype.cull.call({_frame:-1,pos:position,kind0:[instance.kind0],batch},camera,1);
  const dx=position[0]-camera.position.x,dy=position[1]-camera.position.y,dz=position[2]-camera.position.z;
  const distance=Math.sqrt(dx*dx+dy*dy+dz*dz);
  const bits=value=>{const bytes=Buffer.alloc(8);bytes.writeDoubleLE(value);return bytes.readBigUInt64LE().toString(16).padStart(16,'0');};
  cases.push({ordinal,source_id:instance.source_id,length,distance,length_bits:bits(length),distance_bits:bits(distance),frustum_intersects:visible,away,draws:draws.filter(draw=>draw.lod!==2),shadow:draws.some(draw=>draw.lod===2)});
 }
}
if(git('status','--porcelain','--untracked-files=no'))throw Error('Reference changed');
const sourceFiles={};
for(const name of ['src/world/fish/FishProps.js','src/materials/LODFade.js']){const bytes=fs.readFileSync(path.join(source,name));sourceFiles[name]={bytes:bytes.length,sha256:sha(bytes)};}
const descriptor={schema:'forge.tidewater.fish-lod-reference',version:1,source_commit:commit,package_sha256:sha(manifestBytes),intact_source_cull_executed:true,source_files:sourceFiles,cases,limits:['CPU batch selection oracle; no GPU draw or temporal visual acceptance']};
fs.mkdirSync(output);const bytes=Buffer.from(JSON.stringify(descriptor,null,2));fs.writeFileSync(path.join(output,'reference.json'),bytes,{flag:'wx'});
console.log(JSON.stringify({cases:cases.length,reference_sha256:sha(bytes),source_unchanged:true}));process.exit(0);
