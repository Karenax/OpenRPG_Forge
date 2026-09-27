// Pinned source FMM oracle evaluated on canonical corner corrections.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
const [root, archivePath, output] = process.argv.slice(2);
if (!root || !archivePath || !output || fs.existsSync(output)) throw Error('ROOT ARCHIVE NEW_DIRECTORY required');
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const original = fs.readFileSync(path.join(root,'upstream/src/world/ShoreField.js'),'utf8');
if (hash(Buffer.from(original)) !== '0948b2b3b38eb8bf09438052a86dac1c2effd945eb8c5c0aad9746d540c51ba2') throw Error('source pin');
const importLine = "import { GRAVITY } from '../engine/render/Frame.js';";
if (!original.startsWith(importLine)) throw Error('source import');
const frame = fs.readFileSync(path.join(root,'upstream/src/engine/render/Frame.js'),'utf8');
if (!frame.includes('export const GRAVITY = 9.81;')) throw Error('source gravity');
const code = original.replace(importLine,'const GRAVITY = 9.81;');
const {computeShoreField} = await import('data:text/javascript;base64,'+Buffer.from(code).toString('base64'));
const raw = fs.readFileSync(path.join(root,'source-017/terrain/heights.bin'));
if (hash(raw) !== 'be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3') throw Error('height pin');
const heights = new Float32Array(raw.buffer.slice(raw.byteOffset,raw.byteOffset+raw.byteLength));
const archive = JSON.parse(fs.readFileSync(archivePath));
const doc = structuredClone(archive.head.base.terrain);
doc.height_sampling.base_elevation_m = archive.head.corrections.base_elevation_m ?? doc.height_sampling.base_elevation_m;
doc.height_deltas = archive.head.corrections.height_deltas ?? doc.height_deltas ?? [];
const corners = new Float64Array(2049*2049);
for (const d of doc.height_deltas) corners[d.sample_z*2049+d.sample_x] = d.delta_m;
function heightAt(x,z) {
  const fx=x+1023.5, fz=z+1023.5;
  let base=-90;
  if (fx>=0 && fz>=0 && fx<2047 && fz<2047) {
    const i=Math.floor(fx),j=Math.floor(fz),tx=fx-i,tz=fz-j,k=j*2048+i;
    base=(heights[k]*(1-tx)+heights[k+1]*tx)*(1-tz)+(heights[k+2048]*(1-tx)+heights[k+2049]*tx)*tz;
  }
  base+=doc.height_sampling.base_elevation_m;
  const gx=x+1024,gz=z+1024;
  if (gx<0 || gz<0 || gx>2048 || gz>2048) return base;
  const i=Math.min(Math.floor(gx),2047),j=Math.min(Math.floor(gz),2047),tx=gx-i,tz=gz-j,k=j*2049+i;
  return base+(corners[k]*(1-tx)+corners[k+1]*tx)*(1-tz)+(corners[k+2049]*(1-tx)+corners[k+2050]*tx)*tz;
}
const length=Math.sqrt(.12*.12+1);
const {data}=computeShoreField({size:2048,origin:-1024,heightAt},{res:512,swellDir:[-.12/length,-1/length]});
const bytes=Buffer.from(data.buffer);
const source=fs.readFileSync(path.join(root,'workspace-terrain-scene-001/shore-field.rgba32.bin'));
let changed=0;
for(let i=0;i<data.length;i++) if(bytes.readUInt32LE(i*4)!==source.readUInt32LE(i*4)) changed++;
const report={source_sha256:hash(Buffer.from(original)),field_sha256:hash(bytes),changed_values:changed,values:data.length};
fs.mkdirSync(output);
fs.writeFileSync(path.join(output,'terrain.json'),JSON.stringify(doc));
fs.writeFileSync(path.join(output,'field.bin'),bytes);
fs.writeFileSync(path.join(output,'oracle.json'),JSON.stringify(report,null,2));
console.log(JSON.stringify(report));
