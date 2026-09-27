// Execute the pinned pure source bake against the same saved relief corrections.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
const [root, output, archivePath] = process.argv.slice(2);
if (!root || !output || fs.existsSync(output)) throw Error('ROOT NEW_REPORT required');
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const sourcePath = path.join(root, 'upstream/src/world/terrain/TerrainBake.js');
const original = fs.readFileSync(sourcePath, 'utf8');
if (hash(Buffer.from(original)) !== '1a3d72d6fd75528b73b2f7d68edd42fbff7f3a57b82dddfd5e45afdb4ad18431') throw Error('terrain bake source pin');
const importLine = "import { DataUtils } from '../../engine/index.js';";
if (!original.startsWith(importLine)) throw Error('unexpected source import');
// Only the pure bake is called; the unrelated half-float helper is not executed.
const code = original.replace(importLine, '');
const {bakeTerrainMaps} = await import('data:text/javascript;base64,'+Buffer.from(code).toString('base64'));
const raw = fs.readFileSync(path.join(root, 'source-017/terrain/heights.bin'));
if (hash(raw) !== 'be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3') throw Error('source height pin');
const base = new Float32Array(raw.buffer.slice(raw.byteOffset,raw.byteOffset+raw.byteLength));
const archive = JSON.parse(fs.readFileSync(archivePath || path.join(root,'authored-relief-states-001/raised.json')));
const elevation = archive.head.corrections.base_elevation_m ?? archive.head.base.terrain.height_sampling.base_elevation_m;
const corners = new Float64Array(2049*2049);
for (const d of archive.head.corrections.height_deltas ?? archive.head.base.terrain.height_deltas ?? []) corners[d.sample_z*2049+d.sample_x] = d.delta_m;
const heights = new Float32Array(base.length);
for (let z=0;z<2048;z++) for(let x=0;x<2048;x++) {
  const p=z*2049+x;
  heights[z*2048+x] = base[z*2048+x]+elevation+(corners[p]+corners[p+1]+corners[p+2049]+corners[p+2050])*.25;
}
const terrain = {res:2048,heights,texel:1,origin:-1024,rock:new Float32Array(base.length),
  sand:new Uint8Array(base.length),path:new Uint8Array(base.length),gully:new Uint8Array(base.length),pads:[]};
const {normal} = bakeTerrainMaps(terrain);
const expected = fs.readFileSync(path.join(root,'workspace-terrain-scene-001/terrain-normal.bin')).subarray(0,base.length*4);
const ao = new Uint8Array(base.length);
let changed=0;
for(let i=0;i<base.length;i++) {
  ao[i]=normal[i*4+3];
  if(ao[i]!==expected[i*4+3]) changed++;
  expected[i*4]=normal[i*4]; expected[i*4+1]=normal[i*4+1]; expected[i*4+3]=ao[i];
}
const report={source_file_sha256:hash(Buffer.from(original)),ao_sha256:hash(ao),
  changed_ao_texels:changed,normal_base_sha256:hash(expected),samples:ao.length,
  scope:'Pinned JavaScript pure bake oracle for edited heights; renderer verified separately'};
fs.writeFileSync(output,JSON.stringify(report,null,2),{flag:'wx'});
console.log(JSON.stringify(report,null,2));
