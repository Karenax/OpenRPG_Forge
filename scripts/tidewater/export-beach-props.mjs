// SPDX-License-Identifier: MIT
// Tidewater source copyright 2026 DRG Software Solutions LLC, commit 1438b1ab.
// Observation only: preserve every source random call and require identical final geometry.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {pathToFileURL} from 'node:url';

const [sourceArg, outputArg] = process.argv.slice(2);
if (!sourceArg || !outputArg) throw Error('SOURCE NEW_EXTERNAL_DIRECTORY required');
if(process.versions.node!=='22.22.3')throw Error('Pinned Node 22.22.3 required to reproduce the original scene digest');
const source=path.resolve(sourceArg), output=path.resolve(outputArg);
const pin='1438b1abfcaee3267092b75573014f4d9b4a983c';
const git=(...args)=>execFileSync('git',['-C',source,...args],{encoding:'utf8'}).trim();
if(git('rev-parse','HEAD')!==pin || git('status','--porcelain','--untracked-files=no'))
  throw Error('Unmodified pinned source required');
const rel=path.relative(source,output);
if(fs.existsSync(output)||!rel||(!rel.startsWith('..')&&!path.isAbsolute(rel)))
  throw Error('Fresh external output required');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const at=f=>import(pathToFileURL(path.join(source,f)).href);
await at('test/headless.mjs');
const {GPU}=await at('src/engine/gpu/GPU.js');
await GPU.init({headless:true});
const {Scene}=await at('src/engine/index.js');
const {TerrainData}=await at('src/world/TerrainData.js');
const {Colliders}=await at('src/world/Colliders.js');
const {Village}=await at('src/world/Village.js');
const build=Type=>{const terrain=new TerrainData(),colliders=new Colliders();
  return {terrain,colliders,village:new Type({scene:new Scene(),terrain,colliders})};};
const baseline=build(Village);
const original=fs.readFileSync(path.join(source,'src/world/Village.js'),'utf8');
let code=original.replace(/\r\n/g,'\n');
const first=code.indexOf('\t_beachProps( ctx ) {'), last=code.indexOf('\t_villageProps( ctx ) {',first);
if(first<0||last<first)throw Error('Pinned beach prop method not found');
let method=code.slice(first,last);
const replaceOnce=(from,to)=>{if(method.split(from).length!==2)throw Error(`Source marker changed: ${from}`);method=method.replace(from,to);};
replaceOnce("\t\t\tconst gy = g( b.x, b.z );", "\t\t\tconst observation=beginProp('rowboat',ctx,{...b});\n\t\t\tconst gy = g( b.x, b.z );");
replaceOnce("this.footprints.push( { x: b.x, z: b.z, r: 2.4, kind: 'prop' } );", "this.footprints.push( { x: b.x, z: b.z, r: 2.4, kind: 'prop' } );\n\t\t\tendProp(observation,ctx);");
replaceOnce("\t\t\tconst bx = 84.6", "\t\t\tconst boatObservation=beginProp('rowboat',ctx,{position:[84.6,-53.2]});\n\t\t\tconst bx = 84.6");
replaceOnce("\t\t\tconst wx = 102.5", "\t\t\tendProp(boatObservation,ctx);\n\t\t\tconst wreckObservation=beginProp('wreck',ctx,{position:[102.5,-49.8]});\n\t\t\tconst wx = 102.5");
replaceOnce("this.footprints.push( { x: wx, z: wz, r: 4, kind: 'prop' } );", "this.footprints.push( { x: wx, z: wz, r: 4, kind: 'prop' } );\n\t\t\tendProp(wreckObservation,ctx);");
let oars=0;
method=method.replace(/^(\s*)oar\( B, (.*) \);$/gm,(_,space,args)=>{oars++;return `${space}{ const observation=beginProp('oar',ctx,{}); oar( B, ${args} ); endProp(observation,ctx); }`;});
if(oars!==2)throw Error('Two beach oars required');
replaceOnce("\t\t\tconst cy = Math.cos( ry )", "\t\t\tconst observation=beginProp('netRack',ctx,{x,z,yaw:ry});\n\t\t\tconst cy = Math.cos( ry )");
replaceOnce("this.footprints.push( { x, z, r: 2.2, kind: 'prop' } );", "this.footprints.push( { x, z, r: 2.2, kind: 'prop' } );\n\t\t\tendProp(observation,ctx);");
replaceOnce("\t\tfishRack( B, 73.8", "\t\tconst fishObservation=beginProp('fishRack',ctx,{position:[73.8,-69.5]});\n\t\tfishRack( B, 73.8");
replaceOnce("\t\tconst X = PIER.x;", "\t\tendProp(fishObservation,ctx);\n\t\tconst X = PIER.x;");
replaceOnce("\t\t\t\tconst gy = g( x, z );", "\t\t\t\tconst observation=beginProp(type,ctx,{x,z,yaw:ry,stack:stack??0});\n\t\t\t\tconst gy = g( x, z );");
replaceOnce("\n\t\t\t}\n\n\t\t};", "\n\t\t\t\tendProp(observation,ctx);\n\t\t\t}\n\n\t\t};");
replaceOnce("\t\t\tconst [ a, b ] = rand.pick( pal );", "\t\t\tconst observation=beginProp('buoy',ctx,{x,z});\n\t\t\tconst [ a, b ] = rand.pick( pal );");
replaceOnce("buoy( B, x, g( x, z ) - 0.02, z, a, b, rand.chance( 0.5 ) ? 0 : 2, rand.next(), { rz: 1.4, ry: rand.range( 0, 6 ) } );", "buoy( B, x, g( x, z ) - 0.02, z, a, b, rand.chance( 0.5 ) ? 0 : 2, rand.next(), { rz: 1.4, ry: rand.range( 0, 6 ) } );\n\t\t\tendProp(observation,ctx);");
let ropes=0;
method=method.replace(/^(\s*)ropeCoil\( B, (.*) \);$/gm,(_,space,args)=>{ropes++;return `${space}{ const observation=beginProp('ropeCoil',ctx,{}); ropeCoil( B, ${args} ); endProp(observation,ctx); }`;});
if(ropes!==2)throw Error('Two beach rope coils required');
replaceOnce("\t\tfor ( let i = 0; i < 5; i ++ ) {", "\t\tconst fenceObservation=beginProp('buoyFence',ctx,{});\n\t\tfor ( let i = 0; i < 5; i ++ ) {");
const close=method.lastIndexOf('\n\t}\n');
if(close<0)throw Error('Beach method closing marker missing');
method=method.slice(0,close)+'\n\t\tendProp(fenceObservation,ctx);'+method.slice(close);
code=code.slice(0,first)+method+code.slice(last);
code=code.replace(/from\s+(['"])(\.[^'"]+)\1/g,(_,q,p)=>`from ${JSON.stringify(pathToFileURL(path.resolve(source,'src/world',p)).href)}`);
const prelude=`
export const captured=[];
const widths={pos:3,nrm:3,uv:2,tint:3,data:4};
function beginProp(kind,ctx,args){return {kind,args,before:Object.fromEntries(Object.entries(ctx.B.batches).map(([k,b])=>[k,{vertices:b.vcount,indices:b.idx.length}])),boxes:ctx.colliders.boxes.length,cylinders:ctx.colliders.cylinders.length,lights:ctx.lights.length};}
function endProp(record,ctx){const parts={};for(const [material,b] of Object.entries(ctx.B.batches)){const start=record.before[material]??{vertices:0,indices:0};if(b.vcount===start.vertices)continue;const attributes={};for(const [name,width]of Object.entries(widths))attributes[name]=new Float32Array(b[name].slice(start.vertices*width));const indices=new Uint32Array(b.idx.slice(start.indices).map(i=>i-start.vertices));if(indices.some(i=>i>=b.vcount-start.vertices))throw Error('Index ownership violation');parts[material]={start,vertices:b.vcount-start.vertices,attributes,indices,sourceVdata:new Float64Array(b.data.slice(start.vertices*4))};}captured.push({kind:record.kind,args:record.args,parts,colliders:{boxes:ctx.colliders.boxes.slice(record.boxes).map(b=>({center:b.center.toArray(),half:b.half.toArray(),yaw:b.rotY,walkable:b.walkable,solid:b.solid,tag:b.tag})),cylinders:ctx.colliders.cylinders.slice(record.cylinders).map(c=>({...c}))},lights:ctx.lights.slice(record.lights).map(l=>({position:l.position.toArray(),color:l.color.toArray(),intensity:l.intensity,kind:l.kind}))});}
`;
code=prelude+code;
const observed=await import('data:text/javascript;base64,'+Buffer.from(code).toString('base64'));
const instrumented=build(observed.Village);
// Reuse the already audited digest implementation, without executing its exporter.
const digestSource=fs.readFileSync(new URL('./export-plaza-props.mjs',import.meta.url),'utf8');
const digestStart=digestSource.indexOf('function digest('), digestEnd=digestSource.indexOf('\nconst a = digest(',digestStart);
if(digestStart<0||digestEnd<0)throw Error('Digest helper changed');
const digest=Function('hash',`return (${digestSource.slice(digestStart,digestEnd)})`)(hash);
const a=digest(baseline),b=digest(instrumented);
if(JSON.stringify(a)!==JSON.stringify(b))throw Error('Observation changed source scene');
if(hash(JSON.stringify(a))!=='e2f4799bb6f425afb4b9b800fb547528a037da15ad95f8c9d2182889c1e805f1')throw Error('Scene differs from the original pinned acquisition');
if(observed.captured.length!==43)throw Error(`Expected 43 distinct props, got ${observed.captured.length}`);
fs.mkdirSync(output);
const write=(name,bytes)=>{fs.writeFileSync(path.join(output,name),bytes,{flag:'wx'});return {file:name,bytes:bytes.length,sha256:hash(bytes)};};
const props=observed.captured.map((r,i)=>{const parts={};for(const [key,p]of Object.entries(r.parts)){const stem=`prop-${i}-${key}`,attributes={};for(const [n,data]of Object.entries(p.attributes))attributes[n]=write(`${stem}-${n}.bin`,Buffer.from(data.buffer));parts[key]={start:p.start,vertices:p.vertices,attributes,indices:write(`${stem}-indices.bin`,Buffer.from(p.indices.buffer)),source_vdata:write(`${stem}-vdata-f64.bin`,Buffer.from(p.sourceVdata.buffer))};}return {source_id:`tidewater:${pin}:beach-prop:${i}`,kind:r.kind,args:r.args,parts,colliders:r.colliders,lights:r.lights};});
write('LICENSE.txt',fs.readFileSync(path.join(source,'LICENSE')));
write('beach-props.json',Buffer.from(JSON.stringify({schema:'forge.tidewater.beach-parts',version:1,source_commit:pin,source_file_sha256:hash(original),observed_module_sha256:hash(code),source_scene_digest:hash(JSON.stringify(a)),unchanged_merged_scene:true,unchanged_terrain_and_colliders:true,props,limits:['World-space acquisition; portable material package and Studio integration pending','FishProps instance geometry requires separate acquisition','Fence posts and connecting ropes are one logical source object']},null,2)));
if(git('status','--porcelain','--untracked-files=no'))throw Error('Source changed');
console.log(JSON.stringify({props:props.length,parts:props.reduce((n,p)=>n+Object.keys(p.parts).length,0),source_scene_digest:hash(JSON.stringify(a)),source_unchanged:true}));
GPU.device.destroy();
process.exit(0);
