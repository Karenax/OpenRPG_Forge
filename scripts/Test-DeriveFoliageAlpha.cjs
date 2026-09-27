const {test} = require('node:test');
const assert = require('node:assert/strict');
const {parseGlb, cutout} = require('./Derive-FoliageAlpha.cjs');

function fixture(edit = () => {}) {
  const model = {asset:{version:'2.0'}, materials:[{name:'tagged_leaf', alphaMode:'BLEND',
    doubleSided:true, extras:{forge_foliage_alpha:true, forge_foliage_alpha_mode:'blend'},
    pbrMetallicRoughness:{baseColorTexture:{index:0}}}, {name:'untagged_glass',alphaMode:'BLEND'}]};
  edit(model);
  const raw = Buffer.from(JSON.stringify(model));
  const text = Buffer.alloc(Math.ceil(raw.length / 4) * 4, 32);
  raw.copy(text);
  const header = Buffer.alloc(20), binary = Buffer.from([4,0,0,0,66,73,78,0,1,2,3,4]);
  header.writeUInt32LE(0x46546c67,0); header.writeUInt32LE(2,4);
  header.writeUInt32LE(20+text.length+binary.length,8);
  header.writeUInt32LE(text.length,12); header.writeUInt32LE(0x4e4f534a,16);
  return Buffer.concat([header,text,binary]);
}
test('only tagged coverage changes; binary, textures, unrelated glass stay exact', () => {
  const source=fixture(), before=Buffer.from(source), result=cutout(source), parsed=parseGlb(result.bytes);
  assert(source.equals(before));
  assert.equal(result.edits.length,1);
  assert.equal(parsed.model.materials[0].alphaMode,'MASK');
  assert.equal(parsed.model.materials[0].alphaCutoff,0.5);
  assert.equal(parsed.model.materials[1].alphaMode,'BLEND');
  assert(parsed.remainder.equals(parseGlb(source).remainder));
  assert(result.bytes.equals(cutout(source).bytes));
});
test('repeat conversion fails rather than reinterpreting an already changed source',()=>{
  assert.throws(()=>cutout(cutout(fixture()).bytes));
});
test('external resource is refused',()=>assert.throws(()=>cutout(fixture(m=>{m.images=[{uri:'outside.png'}];}))));
test('missing coverage and no tagged foliage are refused',()=>{
  assert.throws(()=>cutout(fixture(m=>delete m.materials[0].pbrMetallicRoughness.baseColorTexture)));
  assert.throws(()=>cutout(fixture(m=>delete m.materials[0].extras.forge_foliage_alpha)));
});
test('truncated or corrupt GLB is refused',()=>{
  assert.throws(()=>parseGlb(Buffer.alloc(0)));
  assert.throws(()=>parseGlb(fixture().subarray(0,40)));
  const bad=fixture(); bad.writeUInt32LE(0xffffffff,12);
  assert.throws(()=>parseGlb(bad));
});
