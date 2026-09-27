// Negative tests use fresh E: fixtures; original assets remain read-only.
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const assert = require('node:assert/strict');
const {spawnSync} = require('node:child_process');
const [kit, validator, evidence] = process.argv.slice(2).map(p => path.resolve(p));
const audit = path.join(__dirname, 'Audit-Gate1Kit.cjs');
const original = JSON.parse(fs.readFileSync(path.join(kit, 'build.json')));
const result = JSON.parse(fs.readFileSync(evidence));
assert.equal(result.errors, 0);
assert.equal(result.assets.length, original.assets.length);
assert(result.assets.every(a => a.lod_strictly_decreasing && a.collision_nodes.length));
const before = fs.readFileSync(evidence);
assert.notEqual(spawnSync(process.execPath, [audit, kit, validator, evidence]).status, 0);
assert.deepEqual(fs.readFileSync(evidence), before);
for (const corruption of ['hash', 'escape', 'absolute']) {
  const temp = fs.mkdtempSync(path.join(path.dirname(evidence), 'negative-'));
  try {
    const build = structuredClone(original);
    build.assets = [build.assets[0]];
    const asset = build.assets[0];
    const source = path.join(kit, asset.glb.path);
    if (corruption === 'hash') {
      fs.copyFileSync(source, path.join(temp, 'asset.glb'));
      asset.glb.path = 'asset.glb';
      asset.glb.sha256 = '0'.repeat(64);
    } else {
      asset.glb.path = corruption === 'absolute' ? source : path.relative(temp, source);
    }
    fs.writeFileSync(path.join(temp, 'build.json'), JSON.stringify(build), {flag:'wx'});
    const output = path.join(temp, 'audit.json');
    const run = spawnSync(process.execPath, [audit, temp, validator, output], {timeout:30000});
    assert.equal(run.error, undefined);
    assert.notEqual(run.status, 0);
    assert(!fs.existsSync(output));
  } finally {
    // Exact directory returned by mkdtemp beneath the supplied evidence directory.
    fs.rmSync(temp, {recursive:true});
  }
}
console.log('PASS current audit + overwrite/hash/escape/absolute rejection (4 negatives)');
