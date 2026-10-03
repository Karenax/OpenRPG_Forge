// Observe the three path-light calls without altering the pinned village.
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
const git = (...args) => execFileSync('git', ['-C', source, ...args], {encoding: 'utf8'}).trim();
if (git('rev-parse', 'HEAD') !== pin || git('status', '--porcelain', '--untracked-files=no'))
  throw Error('pinned source with unchanged tracked files required');
const relative = path.relative(source, output);
if (fs.existsSync(output) || !relative || (!relative.startsWith('..') && !path.isAbsolute(relative)))
  throw Error('new external output required');
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const at = file => import(pathToFileURL(path.join(source, file)).href);
await at('test/headless.mjs');
const {GPU} = await at('src/engine/gpu/GPU.js');
await GPU.init({headless: true});
const {Scene} = await at('src/engine/index.js');
const {TerrainData} = await at('src/world/TerrainData.js');
const {Colliders} = await at('src/world/Colliders.js');
const {Village: OriginalVillage} = await at('src/world/Village.js');
const build = Type => {
  const terrain = new TerrainData(), colliders = new Colliders();
  return {terrain, colliders, village: new Type({scene: new Scene(), terrain, colliders})};
};
const baseline = build(OriginalVillage);
const original = fs.readFileSync(path.join(source, 'src/world/Village.js'), 'utf8');
const imports = 'bench, ropeCoil, pathLight, lampPost, bucket, wreck,';
if (!original.includes(imports)) throw Error('plaza prop imports changed');
let code = original.replace(imports,
  'bench, ropeCoil, pathLight as original_pathLight, lampPost, bucket, wreck,');
code = code.replace(/from\s+(['"])(\.[^'"]+)\1/g,
  (_, q, p) => `from ${JSON.stringify(pathToFileURL(path.resolve(source, 'src/world', p)).href)}`);
const prelude = `
export const capturedPathLights=[];
const widths={pos:3,nrm:3,uv:2,tint:3,data:4};
export function observe(kind,B,args,fn){
 const before=Object.fromEntries(Object.entries(B.batches).map(([key,b])=>
   [key,{vertices:b.vcount,indices:b.idx.length}]));
 const result=fn();
 const parts={};
 for(const [material,b] of Object.entries(B.batches)){
  const start=before[material]??{vertices:0,indices:0};
  if(b.vcount===start.vertices)continue;
  const attributes={};
  for(const [name,width] of Object.entries(widths))
    attributes[name]=new Float32Array(b[name].slice(start.vertices*width));
  const indices=new Uint32Array(b.idx.slice(start.indices).map(i=>i-start.vertices));
  if(indices.some(i=>i>=b.vcount-start.vertices))throw Error('plaza index outside emitted vertices');
  parts[material]={start,vertices:b.vcount-start.vertices,attributes,indices,
    sourceVdata:new Float64Array(b.data.slice(start.vertices*4))};
 }
 capturedPathLights.push({kind,args:JSON.parse(JSON.stringify(args)),parts,live:result,frame:[...B.frame.elements]});
 return result;
}
const pathLight=(B,...args)=>observe('pathLight',B,args,()=>original_pathLight(B,...args));
`;
code = prelude + code;
const observed = await import('data:text/javascript;base64,' + Buffer.from(code).toString('base64'));
let observedPathColliders;
const originalVillageProps=observed.Village.prototype._villageProps;
observed.Village.prototype._villageProps=function(ctx) {
 const before=ctx.colliders.cylinders.length;
 const result=originalVillageProps.call(this,ctx);
 observedPathColliders=ctx.colliders.cylinders.slice(before).filter(c=>c.tag==='pathLight');
 return result;
};
const instrumented = build(observed.Village);
function digest({terrain, colliders, village}) {
  village.group.updateMatrixWorld(true);
  const nodes = [];
  village.group.traverse(node => {
    if (!node.geometry) return;
    const g = node.geometry;
    const attributes = Object.fromEntries(Object.entries(g.attributes).map(([name, attr]) => {
      const data = attr.isInterleavedBufferAttribute ? attr.data.array : attr.array;
      return [name, {hash: hash(Buffer.from(data.buffer, data.byteOffset, data.byteLength)),
        count: attr.count, itemSize: attr.itemSize}];
    }));
    const idx = g.index?.array;
    nodes.push({name: node.name, matrix: Array.from(node.matrixWorld.elements),
      attributes, indices: idx ? hash(Buffer.from(idx.buffer, idx.byteOffset, idx.byteLength)) : null,
      drawRange: g.drawRange});
  });
  return {terrain: hash(Buffer.from(terrain.heights.buffer)), nodes,
    buildings: village.buildings, footprints: village.footprints,
    boxes: colliders.boxes.map(b => [b.center.toArray(), b.half.toArray(), b.rotY,
      b.walkable, b.solid, b.tag]), cylinders: colliders.cylinders};
}
const a = digest(baseline), b = digest(instrumented);
if (JSON.stringify(a) !== JSON.stringify(b))
  throw Error('observation changed village geometry, terrain or colliders');
if (a.terrain !== 'be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3')
  throw Error('terrain differs from pinned acquisition');
if (observed.capturedPathLights.length !== 3) throw Error('path-light call inventory changed');
const rawLights=instrumented.village.getLightSources();
const pathColliders=observedPathColliders;
if(pathColliders.length!==3) throw Error('path-light collider inventory changed: '+JSON.stringify(pathColliders));
for(const [i,record] of observed.capturedPathLights.entries()) {
 const matches=rawLights.filter(light=>light.position===record.live);
 if(matches.length!==1 || matches[0].kind!=='pathLight') throw Error('ambiguous source vector identity');
 const c=pathColliders[i];
 if(c.x!==record.args[0] || c.z!==record.args[2]) throw Error('source loop collider order differs');
 record.light={position:record.live.toArray(),color:matches[0].color.toArray(),
   intensity:matches[0].intensity,kind:matches[0].kind};
 record.collider={...c};
}
fs.mkdirSync(output);
const write = (name, bytes) => {
  fs.writeFileSync(path.join(output, name), bytes, {flag: 'wx'});
  return {file: name, bytes: bytes.length, sha256: hash(bytes)};
};
const props = observed.capturedPathLights.map((record, i) => {
  const parts = {};
  for (const [material, part] of Object.entries(record.parts)) {
    const attributes = Object.fromEntries(Object.entries(part.attributes).map(([name, values]) =>
      [name, write(`${i}-${material}-${name}.bin`, Buffer.from(values.buffer))]));
    parts[material] = {start: part.start, vertices: part.vertices, attributes,
      indices: write(`${i}-${material}-indices.bin`, Buffer.from(part.indices.buffer)),
      source_vdata: write(`${i}-${material}-source-vdata.f64.bin`,
        Buffer.from(part.sourceVdata.buffer))};
  }
  return {source_id: `tidewater:${pin}:village-path-light:${i}`, kind: record.kind,
    args: record.args, construction_frame:record.frame,
    colliders:{boxes:[],cylinders:[record.collider]}, lights:[record.light], parts};
});
const report = {schema: 'forge.tidewater.path-light-parts', version: 1, source_commit: pin,
  source_file_sha256: hash(original), observed_module_sha256: hash(code),
  source_scene_digest: hash(JSON.stringify(a)), unchanged_merged_scene: true,
  unchanged_terrain_and_colliders: true, terrain_sha256: a.terrain, props,
  source_inputs: ['src/world/Village.js','src/world/Props.js','LICENSE'].map(file=>({file,sha256:hash(fs.readFileSync(path.join(source,file)))})),
  limits: ['world-space geometry and source colliders',
    'requires material fold and portable package',
    'not yet a Studio editable asset or rendered Playtest proof']};
write('path-lights.json', Buffer.from(JSON.stringify(report, null, 2)));
if (git('status', '--porcelain', '--untracked-files=no')) throw Error('source was modified');
console.log(JSON.stringify({props: props.length,
  parts: props.reduce((sum, p) => sum + Object.keys(p.parts).length, 0),
  source_scene_digest: report.source_scene_digest, unchanged_merged_scene: true}));
GPU.device.destroy();
process.exit(0);
