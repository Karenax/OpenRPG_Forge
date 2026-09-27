// Read-only audit of an exact Forge kit. No promotion, source edit or network.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');
const sha = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
async function main() {
  const [kitArg, validatorArg, outputArg] = process.argv.slice(2);
  const root = fs.realpathSync(kitArg), output = path.resolve(outputArg);
  assert(!fs.existsSync(output), 'fresh output required');
  const validator = require(path.resolve(validatorArg));
  const buildBytes = fs.readFileSync(path.join(root, 'build.json'));
  const build = JSON.parse(buildBytes);
  assert.equal(build.schema, 'forge.blender-asset-kit-build');
  assert(build.assets.length > 0 && build.assets.length <= 64);
  const assets = [];
  for (const asset of build.assets) {
    assert(!path.isAbsolute(asset.glb.path) && !asset.glb.path.includes(':'), 'relative GLB required');
    const file = fs.realpathSync(path.join(root, asset.glb.path));
    const relative = path.relative(root, file);
    assert(!relative.startsWith('..') && !path.isAbsolute(relative), 'GLB escapes kit');
    assert(fs.statSync(file).size <= 256 * 1024 * 1024, 'GLB too large');
    const bytes = fs.readFileSync(file);
    assert.equal(sha(bytes), asset.glb.sha256);
    const validation = await validator.validateBytes(new Uint8Array(bytes), {
      uri: asset.glb.path, maxIssues: 1000,
      externalResourceFunction: () => Promise.reject(Error('external resources forbidden'))
    });
    assert.equal(bytes.readUInt32LE(0), 0x46546c67);
    assert.equal(bytes.readUInt32LE(4), 2);
    assert.equal(bytes.readUInt32LE(8), bytes.length);
    assert.equal(bytes.readUInt32LE(16), 0x4e4f534a);
    const doc = JSON.parse(bytes.subarray(20, 20 + bytes.readUInt32LE(12)).toString());
    assert(!(doc.buffers || []).some(x => x.uri));
    assert(!(doc.images || []).some(x => x.uri));
    const lodTriangles = [0, 0, 0], collisions = [], nodes = [];
    for (const node of doc.nodes || []) {
      if (node.mesh === undefined) continue;
      const mesh = doc.meshes[node.mesh];
      let triangles = 0;
      for (const primitive of mesh.primitives) {
        assert.equal(primitive.mode ?? 4, 4, 'only triangle primitives supported by this audit');
        const count = doc.accessors[primitive.indices ?? primitive.attributes.POSITION].count;
        assert.equal(count % 3, 0);
        triangles += count / 3;
      }
      const level = /_LOD([012])(?:_|$)/.exec(node.name || '');
      if (level) lodTriangles[Number(level[1])] += triangles;
      if ((node.name || '').startsWith('UCX_')) collisions.push(node.name);
      nodes.push({name: node.name, triangles, translation: node.translation ?? [0,0,0],
        rotation: node.rotation ?? [0,0,0,1], scale: node.scale ?? [1,1,1], matrix: node.matrix ?? null});
    }
    assets.push({id: asset.id, sha256: sha(bytes), bytes: bytes.length, lod_triangles: lodTriangles,
      lod_strictly_decreasing: lodTriangles[0] > lodTriangles[1] && lodTriangles[1] > lodTriangles[2] && lodTriangles[2] > 0,
      collision_nodes: collisions, nodes, material_count: (doc.materials || []).length,
      materials: (doc.materials || []).map(m => ({name:m.name,alpha_mode:m.alphaMode ?? 'OPAQUE',
        pbr:!!m.pbrMetallicRoughness,normal_texture:!!m.normalTexture,occlusion_texture:!!m.occlusionTexture})),
      validation});
    assert.equal(sha(fs.readFileSync(file)), asset.glb.sha256, 'source changed during validation');
  }
  const errors = assets.reduce((n,a) => n+a.validation.issues.numErrors,0);
  const warnings = assets.reduce((n,a) => n+a.validation.issues.numWarnings,0);
  const result = {schema:'forge.gate1-kit-audit',schema_version:1,kit_sha256:sha(buildBytes),
    validator_version:validator.version(),assets,errors,warnings,provenance:build.provenance,
    human_retouch_verified:false,human_pivot_acceptance:false,distribution_qualified:false,
    limits:['LOD counts include each named mesh node, not runtime submitted draws.',
      'Transforms are recorded, not a human pivot or collision acceptance.']};
  fs.mkdirSync(path.dirname(output),{recursive:true});
  fs.writeFileSync(output,JSON.stringify(result,null,2)+'\n',{flag:'wx'});
  console.log(JSON.stringify({assets:assets.length,errors,warnings,
    lod_decreasing:assets.every(a=>a.lod_strictly_decreasing),output}));
  if(errors || assets.some(a=>!a.lod_strictly_decreasing || !a.collision_nodes.length)) process.exitCode=2;
}
main().catch(error => {console.error(error);process.exitCode=1;});
