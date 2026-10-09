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
const first=code.indexOf('\t_villageProps( ctx ) {'), last=code.indexOf('\t// ------------------------------------------------------------------ meshes',first);
if(first<0||last<first)throw Error('Pinned village prop method not found');
let method=code.slice(first,last);
const replaceOnce=(from,to)=>{if(method.split(from).length!==2)throw Error(`Source marker changed: ${from}`);method=method.replace(from,to);};
replaceOnce("\t\t\tfence( B, pts, g, style, tint", "\t\t\tconst observation=beginProp('fence',ctx,{points:pts,style});\n\t\t\tfence( B, pts, g, style, tint");
replaceOnce("for ( const [ x, z ] of pts ) this.foundationChecks.push( { x, y: g( x, z ) - 0.13, z } );", "for ( const [ x, z ] of pts ) this.foundationChecks.push( { x, y: g( x, z ) - 0.13, z } );\n\t\t\tendProp(observation,ctx);");
method=method.replace(/^(\s*)laundryLine\( B, (.*) \);$/gm,(_,space,args)=>`${space}{ const observation=beginProp('laundryLine',ctx,{}); laundryLine( B, ${args} ); endProp(observation,ctx); }`);
replaceOnce("\t\tfishRack( B, 67.0", "\t\tconst fishObservation=beginProp('fishRack',ctx,{position:[67,-118.5]});\n\t\tfishRack( B, 67.0");
replaceOnce("\t\tnetRack( B, 5.5", "\t\tendProp(fishObservation,ctx);\n\t\tconst netObservation=beginProp('netRack',ctx,{position:[5.5,-136.5]});\n\t\tnetRack( B, 5.5");
replaceOnce("\t\tconst clutter = [", "\t\tendProp(netObservation,ctx);\n\t\tconst clutter = [");
replaceOnce("\t\t\tconst gy = g( x, z );", "\t\t\tconst clutterObservation=beginProp(type,ctx,{x,z,yaw:ry,stack:stack??0});\n\t\t\tconst gy = g( x, z );");
replaceOnce("\n\t\t}\n\n\t\t// upturned rowboat", "\n\t\t\tendProp(clutterObservation,ctx);\n\t\t}\n\n\t\t// upturned rowboat");
replaceOnce("\t\trowboat( B, 88.2", "\t\tconst boatObservation=beginProp('rowboatOnTrestles',ctx,{position:[88.2,-124.5],yaw:1.2});\n\t\trowboat( B, 88.2");
replaceOnce("\t\tbucket( B, 86.9", "\t\tendProp(boatObservation,ctx);\n\t\tconst bucketObservation=beginProp('bucket',ctx,{position:[86.9,-122.6]});\n\t\tbucket( B, 86.9");
replaceOnce("\t\t// a couple of extra path lights", "\t\tendProp(bucketObservation,ctx);\n\t\t// a couple of extra path lights");
replaceOnce("\t\t\tconst w = pathLight", "\t\t\tconst lightObservation=beginProp('pathLight',ctx,{x,z});\n\t\t\tconst w = pathLight");
replaceOnce("this.foundationChecks.push( { x, y: g( x, z ) - 0.25, z } );", "this.foundationChecks.push( { x, y: g( x, z ) - 0.25, z } );\n\t\t\tendProp(lightObservation,ctx);");
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
if(observed.captured.length!==30)throw Error(`Expected 30 distinct props, got ${observed.captured.length}`);
fs.mkdirSync(output);
const write=(name,bytes)=>{fs.writeFileSync(path.join(output,name),bytes,{flag:'wx'});return {file:name,bytes:bytes.length,sha256:hash(bytes)};};
const props=observed.captured.map((r,i)=>{const parts={};for(const [key,p]of Object.entries(r.parts)){const stem=`prop-${i}-${key}`,attributes={};for(const [n,data]of Object.entries(p.attributes))attributes[n]=write(`${stem}-${n}.bin`,Buffer.from(data.buffer));parts[key]={start:p.start,vertices:p.vertices,attributes,indices:write(`${stem}-indices.bin`,Buffer.from(p.indices.buffer)),source_vdata:write(`${stem}-vdata-f64.bin`,Buffer.from(p.sourceVdata.buffer))};}return {source_id:`tidewater:${pin}:village-prop:${i}`,kind:r.kind,args:r.args,parts,colliders:r.colliders,lights:r.lights};});
write('LICENSE.txt',fs.readFileSync(path.join(source,'LICENSE')));
write('village-props.json',Buffer.from(JSON.stringify({schema:'forge.tidewater.village-parts',version:1,source_commit:pin,source_file_sha256:hash(original),observed_module_sha256:hash(code),source_scene_digest:hash(JSON.stringify(a)),unchanged_merged_scene:true,unchanged_terrain_and_colliders:true,props,limits:['World-space acquisition; portable material package and Studio integration pending','FishProps instance geometry requires separate acquisition','Path lights overlap their previously acquired owners; no duplicate placement permitted']},null,2)));
if(git('status','--porcelain','--untracked-files=no'))throw Error('Source changed');
console.log(JSON.stringify({props:props.length,parts:props.reduce((n,p)=>n+Object.keys(p.parts).length,0),source_scene_digest:hash(JSON.stringify(a)),source_unchanged:true}));
GPU.device.destroy();
process.exit(0);
