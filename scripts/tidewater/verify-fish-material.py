"""Verify two independent generated FishProps material acquisitions."""
from pathlib import Path
import hashlib,json,math,struct,sys
first,second,source,receipt=map(Path,sys.argv[1:])
assert first.resolve()!=second.resolve()
sha=lambda b:hashlib.sha256(b).hexdigest()
assert {p.name for p in first.iterdir()}=={p.name for p in second.iterdir()}
assert all(p.read_bytes()==(second/p.name).read_bytes() for p in first.iterdir())
d=json.loads((first/'fish-material.json').read_bytes())
assert d['schema']=='forge.tidewater.fish-material-source' and d['version']==1
assert d['source_commit']=='1438b1abfcaee3267092b75573014f4d9b4a983c'
assert d['node_version']=='22.22.3' and d['specular_mode']=='intended'
assert set(d['materials'])=={'main','fade'} and len(d['modules'])==4
assert {p.name for p in first.iterdir()}==set(d['files'])|{'fish-material.json'}
for name,pin in d['files'].items():
 assert Path(name).name==name
 raw=(first/name).read_bytes()
 assert len(raw)==pin['bytes'] and sha(raw)==pin['sha256']
for name,export in d['provenance'].items():
 assert (source/name).read_bytes()==(first/export).read_bytes()
assert b'MIT License' in (first/d['provenance']['LICENSE']).read_bytes()
table=d['skin_table'];rows=table['rows']
assert table['struct']=='FishSkin' and len(rows)==len(d['pattern'])*8
assert len(d['pattern'])==21 and sorted(d['pattern'].values())==list(range(21))
assert all(len(row)==4 and all(math.isfinite(x) for x in row) for row in rows)
raw=b''.join(struct.pack('<4f',*row) for row in rows)
assert len(raw)==table['byte_length']==2688 and raw==(first/table['file']).read_bytes()
for name,m in d['materials'].items():
 assert m['attributes']=={'aData':'vec4f','aKind':'f32'}
 assert m['defines']=={'CLEARCOAT':1} and m['side']=='front'
 assert m['depth_write'] and not m['transparent']
 assert m['roughness']==0.4 and m['metalness']==0
 for field in ['vertex','surface','shadow']:
  code=(first/f'{name}-{field}.wgsl').read_text(encoding='utf-8')
  assert code.strip() and '${' not in code
 assert 'discard' in (first/f'{name}-shadow.wgsl').read_text(encoding='utf-8')
assert not receipt.exists()
r=dict(schema='forge.tidewater.fish-material-source-proof.v1',passed=True,independent_pair=True,files=22,skin_rows=len(rows),skin_bytes=len(raw),descriptor_sha256=sha((first/'fish-material.json').read_bytes()),source_provenance_unchanged=True,gpu_shader_executed=False,rust_integrated=False,visual_parity=False)
receipt.write_text(json.dumps(r,indent=2),encoding='utf-8');print(json.dumps(r))