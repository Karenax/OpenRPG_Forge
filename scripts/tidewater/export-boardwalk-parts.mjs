// Observe the three source boardwalk builds without changing the pinned village.
// SPDX-License-Identifier: MIT
// Tidewater source copyright 2026 DRG Software Solutions LLC, commit 1438b1ab.
import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
import {execFileSync} from 'node:child_process';
import crypto from 'node:crypto';

const [sourceArg, outputArg] = process.argv.slice(2);
if (!sourceArg || !outputArg) throw Error('SOURCE NEW_DIRECTORY required');
const source = path.resolve(sourceArg), output = path.resolve(outputArg);
const pin = '1438b1abfcaee3267092b75573014f4d9b4a983c';
const git = (...args) => execFileSync('git', ['-C', source, ...args], {encoding:'utf8'}).trim();
if (git('rev-parse', 'HEAD') !== pin || git('status', '--porcelain')) throw Error('clean pinned source required');
const relative = path.relative(source, output);
if (fs.existsSync(output) || !relative || (!relative.startsWith('..') && !path.isAbsolute(relative))) throw Error('new external output required');
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const at = file => import(pathToFileURL(path.join(source, file)).href);
await at('test/headless.mjs');
const {GPU} = await at('src/engine/gpu/GPU.js');
await GPU.init({headless:true});
const {Scene} = await at('src/engine/index.js');
const {TerrainData} = await at('src/world/TerrainData.js');
const {Colliders} = await at('src/world/Colliders.js');
const {Village:OriginalVillage} = await at('src/world/Village.js');
const build = Type => {
  const terrain = new TerrainData(), colliders = new Colliders();
  return {terrain, colliders, village:new Type({scene:new Scene(), terrain, colliders})};
};
const baseline = build(OriginalVillage);
const original = fs.readFileSync(path.join(source, 'src/world/Village.js'), 'utf8');
const importLine = "import { buildBoardwalk } from './village/Boardwalk.js';";
if (!original.includes(importLine)) throw Error('boardwalk import changed');
let code = original.replace(importLine,
  "import { buildBoardwalk as original_buildBoardwalk } from './village/Boardwalk.js';");
code = code.replace(/from\s+(['"])(\.[^'"]+)\1/g,
  (_, q, p) => `from ${JSON.stringify(pathToFileURL(path.resolve(source, 'src/world', p)).href)}`);
const prelude = `
export const capturedBoardwalks=[];
const widths={pos:3,nrm:3,uv:2,tint:3,data:4};
function buildBoardwalk(ctx,points,options={}) {
  const before=Object.fromEntries(Object.entries(ctx.B.batches).map(([key,batch])=>
    [key,{vertices:batch.vcount,indices:batch.idx.length}]));
  const boxStart=ctx.colliders.boxes.length,cylinderStart=ctx.colliders.cylinders.length;
  const lightStart=ctx.lights.length,checkStart=ctx.checks.length;
  const result=original_buildBoardwalk(ctx,points,options);
  const parts={};
  for(const [material,batch] of Object.entries(ctx.B.batches)) {
    const start=before[material]??{vertices:0,indices:0};
    if(batch.vcount===start.vertices)continue;
    const attributes={};
    for(const [name,width] of Object.entries(widths))
      attributes[name]=new Float32Array(batch[name].slice(start.vertices*width));
    const indices=new Uint32Array(batch.idx.slice(start.indices).map(i=>i-start.vertices));
    if(indices.some(i=>i>=batch.vcount-start.vertices))throw Error('boardwalk index outside emitted vertices');
    parts[material]={start,vertices:batch.vcount-start.vertices,attributes,indices,
      sourceVdata:new Float64Array(batch.data.slice(start.vertices*4))};
  }
  const boxes=ctx.colliders.boxes.slice(boxStart).map(box=>({
    center:box.center.toArray(),half:box.half.toArray(),yaw:box.rotY,
    walkable:box.walkable,solid:box.solid,tag:box.tag}));
  const cylinders=ctx.colliders.cylinders.slice(cylinderStart).map(c=>({...c}));
  const lights=ctx.lights.slice(lightStart).map(light=>({
    position:light.position.toArray(),color:light.color.toArray(),
    intensity:light.intensity,kind:light.kind}));
  capturedBoardwalks.push({points,options,layout:{length:result.length,width:result.width,
    centerline:result.samples.map((s,i)=>[s.p.x,result.deck[i],s.p.z])},
    boxes,cylinders,lights,checks:ctx.checks.slice(checkStart).map(p=>({...p})),parts});
  return result;
}
`;
code = prelude + code;
const observed = await import('data:text/javascript;base64,' + Buffer.from(code).toString('base64'));
const instrumented = build(observed.Village);
function digest({terrain,colliders,village}) {
  village.group.updateMatrixWorld(true);
  const nodes=[];
  village.group.traverse(node=>{
    if(!node.geometry)return;
    const geometry=node.geometry;
    const attributes=Object.fromEntries(Object.entries(geometry.attributes).map(([name,attr])=>{
      const data=attr.isInterleavedBufferAttribute?attr.data.array:attr.array;
      return [name,{hash:hash(Buffer.from(data.buffer,data.byteOffset,data.byteLength)),
        count:attr.count,itemSize:attr.itemSize}];
    }));
    const idx=geometry.index?.array;
    nodes.push({name:node.name,matrix:Array.from(node.matrixWorld.elements),attributes,
      indices:idx?hash(Buffer.from(idx.buffer,idx.byteOffset,idx.byteLength)):null,
      drawRange:geometry.drawRange});
  });
  return {terrain:hash(Buffer.from(terrain.heights.buffer)),nodes,
    buildings:village.buildings,footprints:village.footprints,
    boxes:colliders.boxes.map(b=>[b.center.toArray(),b.half.toArray(),b.rotY,b.walkable,b.solid,b.tag]),
    cylinders:colliders.cylinders};
}
const a=digest(baseline),b=digest(instrumented);
if(JSON.stringify(a)!==JSON.stringify(b))throw Error('observation changed village geometry, terrain or colliders');
if(a.terrain!=='be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3')
  throw Error('terrain differs from pinned acquisition');
if(observed.capturedBoardwalks.length!==3)throw Error('expected three boardwalk source calls');
fs.mkdirSync(output);
const write=(name,bytes)=>{
  fs.writeFileSync(path.join(output,name),bytes,{flag:'wx'});
  return {file:name,bytes:bytes.length,sha256:hash(bytes)};
};
const boardwalks=observed.capturedBoardwalks.map((record,i)=>{
  const parts={};
  for(const [material,part] of Object.entries(record.parts)){
    const attributes=Object.fromEntries(Object.entries(part.attributes).map(([name,values])=>
      [name,write(`${i}-${material}-${name}.bin`,Buffer.from(values.buffer))]));
    parts[material]={start:part.start,vertices:part.vertices,attributes,
      indices:write(`${i}-${material}-indices.bin`,Buffer.from(part.indices.buffer)),
      source_vdata:write(`${i}-${material}-source-vdata.f64.bin`,Buffer.from(part.sourceVdata.buffer))};
  }
  return {source_id:`tidewater:${pin}:boardwalk:${i}`,role:i===0?'pier-to-plaza':'side-path',
    points:record.points,options:record.options,layout:record.layout,
    colliders:{boxes:record.boxes,cylinders:record.cylinders},
    lights:record.lights,checks:record.checks,parts};
});
const report={schema:'forge.tidewater.boardwalk-parts',version:1,source_commit:pin,
  source_file_sha256:hash(original),observed_module_sha256:hash(code),
  source_scene_digest:hash(JSON.stringify(a)),unchanged_merged_scene:true,
  unchanged_terrain_and_colliders:true,terrain_sha256:a.terrain,boardwalks,
  limits:['world-space geometry and source colliders','requires material fold and portable package',
    'not yet a Studio editable asset or rendered Playtest proof']};
write('boardwalks.json',Buffer.from(JSON.stringify(report,null,2)));
if(git('status','--porcelain'))throw Error('source was modified');
console.log(JSON.stringify({boardwalks:boardwalks.length,
  parts:boardwalks.reduce((sum,record)=>sum+Object.keys(record.parts).length,0),
  boxes:boardwalks.reduce((sum,record)=>sum+record.colliders.boxes.length,0),
  cylinders:boardwalks.reduce((sum,record)=>sum+record.colliders.cylinders.length,0),
  source_scene_digest:report.source_scene_digest,unchanged_merged_scene:true}));
GPU.device.destroy();
process.exit(0);
