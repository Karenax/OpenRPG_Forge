// Local Khronos validation of the exact candidate GLB; no network or promotion.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');
const sha = b => crypto.createHash('sha256').update(b).digest('hex');
async function main() {
  const [rootArg, validatorArg] = process.argv.slice(2);
  const root = fs.realpathSync(rootArg);
  const evidence = JSON.parse(fs.readFileSync(path.join(root, 'derivation.json')));
  assert.equal(path.basename(evidence.glb), evidence.glb);
  const bytes = fs.readFileSync(path.join(root, evidence.glb));
  assert.equal(sha(bytes), evidence.glb_sha256);
  const validator = require(path.resolve(validatorArg));
  const report = await validator.validateBytes(new Uint8Array(bytes), {uri:evidence.glb});
  const output = {glb_sha256:sha(bytes), validator_version:validator.version(), report};
  fs.writeFileSync(path.join(root, 'validation.khronos.json'), JSON.stringify(output,null,2)+'\n', {flag:'wx'});
  console.log(JSON.stringify(report.issues));
  assert.equal(report.issues.numErrors, 0);
}
main().catch(error => { console.error(error); process.exitCode=1; });
