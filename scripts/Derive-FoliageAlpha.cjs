// Local, unpromoted cutout derivative of a governed runtime kit. No network.
// node this-file <source-kit> <fresh-output-kit> <gltf-validator-directory>
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');

const sha = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const json = value => Buffer.from(JSON.stringify(value, null, 2) + '\n');

function parseGlb(bytes) {
  assert(bytes.length >= 28 && bytes.readUInt32LE(0) === 0x46546c67, 'Invalid GLB');
  assert.equal(bytes.readUInt32LE(4), 2);
  assert.equal(bytes.readUInt32LE(8), bytes.length);
  const length = bytes.readUInt32LE(12);
  assert.equal(bytes.readUInt32LE(16), 0x4e4f534a);
  assert(length % 4 === 0 && 20 + length <= bytes.length, 'Invalid JSON chunk');
  return { model: JSON.parse(bytes.subarray(20, 20 + length).toString('utf8')),
    remainder: bytes.subarray(20 + length) };
}

function cutout(bytes) {
  const source = parseGlb(bytes);
  for (const entry of [...(source.model.buffers ?? []), ...(source.model.images ?? [])]) {
    assert(!entry.uri, 'External dependencies are not supported');
  }
  const model = structuredClone(source.model);
  const edits = [];
  for (const [index, material] of (model.materials ?? []).entries()) {
    if (material.extras?.forge_foliage_alpha !== true) continue;
    assert.equal(material.alphaMode, 'BLEND', 'Only explicit source BLEND foliage is supported');
    assert.equal(material.extras.forge_foliage_alpha_mode, 'blend');
    assert(material.pbrMetallicRoughness?.baseColorTexture, 'Missing coverage texture');
    edits.push({ index, name: material.name });
    material.alphaMode = 'MASK';
    material.alphaCutoff = 0.5;
    material.extras.forge_foliage_alpha_mode = 'mask';
  }
  assert(edits.length > 0, 'No explicitly tagged foliage found');
  const encoded = Buffer.from(JSON.stringify(model));
  const padded = Buffer.alloc(Math.ceil(encoded.length / 4) * 4, 32);
  encoded.copy(padded);
  const header = Buffer.alloc(20);
  header.writeUInt32LE(0x46546c67, 0);
  header.writeUInt32LE(2, 4);
  header.writeUInt32LE(20 + padded.length + source.remainder.length, 8);
  header.writeUInt32LE(padded.length, 12);
  header.writeUInt32LE(0x4e4f534a, 16);
  const result = Buffer.concat([header, padded, source.remainder]);
  const reread = parseGlb(result);
  assert(reread.remainder.equals(source.remainder), 'Geometry/image bytes changed');
  for (const edit of edits) reread.model.materials[edit.index] = source.model.materials[edit.index];
  assert.deepEqual(reread.model, source.model, 'Non-material document changes');
  return { bytes: result, edits, binary_chunk_sha256: sha(source.remainder) };
}

function inside(root, relative) {
  const resolved = path.resolve(root, relative);
  assert(resolved.startsWith(root + path.sep), 'Path escapes kit');
  assert(!fs.lstatSync(resolved).isSymbolicLink(), 'Symlinks unsupported');
  return resolved;
}

async function main() {
  const [sourceArg, outputArg, validatorArg] = process.argv.slice(2);
  assert(sourceArg && outputArg && validatorArg, 'Expected source, fresh output, validator');
  const source = fs.realpathSync(sourceArg), output = path.resolve(outputArg);
  assert.equal(path.parse(output).root.toLowerCase(), 'e:\\');
  assert(!fs.existsSync(output), 'Output already exists');
  assert(!output.startsWith(source + path.sep), 'Output cannot be inside source');
  const validator = require(path.resolve(validatorArg));
  const names = ['build.json', 'preview.json', 'prototype-catalog.json'];
  const inputs = new Map(names.map(name => [name, fs.readFileSync(inside(source, name))]));
  const [build, preview, catalog] = names.map(name => JSON.parse(inputs.get(name)));
  assert.equal(build.schema, 'forge.blender-asset-kit-build');
  assert.equal(build.qualification.technical_export_validated, true);
  assert.equal(build.asset_kit_id, preview.asset_kit_id);
  assert.equal(build.asset_kit_id, catalog.asset_kit_id);
  assert.equal(build.asset_kit_revision, preview.asset_kit_revision);
  assert.equal(build.asset_kit_revision, catalog.asset_kit_revision);
  const authored = new Set(build.authored_source?.assets ?? []);
  const files = new Map(), records = [];
  for (const asset of build.assets) {
    const bytes = fs.readFileSync(inside(source, asset.glb.path));
    assert.equal(sha(bytes), asset.glb.sha256, 'Source hash mismatch');
    inputs.set(asset.glb.path, bytes);
    if (asset.kind !== 'tree') { files.set(asset.glb.path, bytes); continue; }
    assert(!authored.has(asset.id), 'Authored tree must not be transformed implicitly');
    const derived = cutout(bytes);
    const validation = await validator.validateBytes(new Uint8Array(derived.bytes), {uri: asset.glb.path});
    assert.equal(validation.issues.numErrors, 0, 'glTF validator errors');
    records.push({ id: asset.id, source_sha256: sha(bytes), derived_sha256: sha(derived.bytes),
      edits: derived.edits, binary_chunk_sha256: derived.binary_chunk_sha256, validation });
    asset.glb.sha256 = sha(derived.bytes);
    files.set(asset.glb.path, derived.bytes);
    for (const binding of catalog.bindings.filter(item => item.asset_id === asset.id)) {
      assert.equal(binding.glb_sha256, sha(bytes));
      binding.glb_sha256 = sha(derived.bytes);
    }
  }
  assert(records.length > 0, 'No trees');
  const evidence = { schema: 'forge.foliage-alpha-derivative', schema_version: 1,
    status: 'STRUCTURAL_AND_GLTF_PASS', validator_version: validator.version(),
    source_kit: source, script_sha256: sha(fs.readFileSync(__filename)),
    sources: Object.fromEntries([...inputs].map(([name, bytes]) => [name, sha(bytes)])),
    scope: 'MASK 0.5 only; no runtime, visual, reimport or human qualification',
    original_qualification: build.qualification, assets: records };
  const evidenceBytes = json(evidence);
  build.asset_kit_id += '-alpha-mask';
  build.asset_kit_revision = 1;
  for (const item of [preview, catalog]) {
    item.asset_kit_id = build.asset_kit_id;
    item.asset_kit_revision = build.asset_kit_revision;
  }
  build.generator.assembly_script_sha256 = evidence.script_sha256;
  build.qualification = { technical_export_validated: true, human_art_review: 'pending',
    publication_approved: false, r1_063_complete: false };
  build.candidate_evidence = { base_report_sha256: sha(inputs.get('build.json')),
    derivative_proof_sha256: sha(evidenceBytes), scope: evidence.scope };
  for (const [name, bytes] of inputs) assert.equal(sha(fs.readFileSync(inside(source, name))), sha(bytes));
  fs.mkdirSync(output, {recursive: true});
  for (const [name, bytes] of files) {
    const target = path.resolve(output, name);
    assert(target.startsWith(output + path.sep));
    fs.mkdirSync(path.dirname(target), {recursive: true});
    fs.writeFileSync(target, bytes, {flag: 'wx'});
  }
  for (const [name, item] of [['build.json', build], ['preview.json', preview], ['prototype-catalog.json', catalog]]) {
    fs.writeFileSync(path.join(output, name), json(item), {flag: 'wx'});
  }
  fs.writeFileSync(path.join(output, 'alpha-derivative-proof.json'), evidenceBytes, {flag: 'wx'});
  console.log(JSON.stringify({status: evidence.status, assets: records.map(r => ({id:r.id, edits:r.edits.length,
    errors:r.validation.issues.numErrors, warnings:r.validation.issues.numWarnings})), output}));
}

module.exports = {parseGlb, cutout};
if (require.main === module) main().catch(error => { console.error(error); process.exitCode = 1; });
