// Development-only acquisition of audited constructor root declarations.
// Full vendor constructors fetch assets; only the pinned pose blocks run here.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { inverseSourceFrame } from './source-village-light-owners.mjs';

const PIN = '1438b1abfcaee3267092b75573014f4d9b4a983c';
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const blocks = [
  { path: 'src/game/FishStand.js', source_sha: '019f032aa9bae21607fa3cf040919093dba024d77e4f7feca602f1aa97eddc80',
    y_line: 24, group_line: 26, block_sha: '588a03fb7ebfc47f3bff8e5ea227f5c5be6fadd949bebf6769399effaa621b25' },
  { path: 'src/game/Chandlery.js', source_sha: '55f52bb83f0b09131f88fa545c1f706afecf47b757a88271f1838c1f4f2136e5',
    y_line: 17, group_line: 19, block_sha: 'e4ea27ef363ee1b6f70b2ea80b8cd90b37405ab5004a6de62b3719c9a3a95eb4' },
];

export function acquireVendorOwnerFrames({ sourceCommit, sourceInputs, originals,
  Group, terrain, STAND, CHANDLERY }) {
  assert.equal(sourceCommit, PIN);
  // Check every pin before executing even the first bounded pose block.
  const prepared = blocks.map(row => {
    const bytes = originals.get(row.path), input = sourceInputs.find(input => input.path === row.path);
    assert(bytes && input);
    assert.equal(input.sha256, row.source_sha); assert.equal(input.bytes, bytes.length);
    assert.equal(hash(bytes), row.source_sha);
    const lines = bytes.toString('utf8').replaceAll('\r\n', '\n').split('\n');
    const code = [lines[row.y_line - 1], ...lines.slice(row.group_line - 1, row.group_line + 3)].join('\n') + '\n';
    assert.equal(hash(code), row.block_sha, 'Audited vendor root declaration changed');
    return { row, code };
  });
  const frames = prepared.map(({ row, code }, ordinal) => {
    const owner = {};
    new Function('Group', 'terrain', 'STAND', 'CHANDLERY', '"use strict";\n' + code)
      .call(owner, Group, terrain, STAND, CHANDLERY);
    return { id: `vendor-${ordinal}`, parent: 'world', source_object: `${row.path}:constructor.group`,
      ...inverseSourceFrame(owner.group), binding: { producer_path: row.path,
        producer_function: 'constructor', collection: 'App.vendor-light-loop', ordinal,
        anchor: 'group', evidence: 'audited-source-root-declaration-and-app-loop-order' } };
  });
  return { frames, receipt: { schema: 'forge.source-vendor-light-owner-observation', version: 1,
    source_commit: PIN, blocks, normalization: 'UTF-8/LF/trailing-newline',
    invocation: 'audited-root-declaration-blocks', full_vendor_constructors_executed: false,
    asset_fetch_executed: false, renderer_parity: false, studio_qualified: false } };
}
