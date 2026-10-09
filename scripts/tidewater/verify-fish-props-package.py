"""Verify portable FishProps package closure and compare independent assemblies."""
from pathlib import Path
import hashlib,json,sys
PIN='e578b44e93d18d79ec06c875b76dae005f71c17ecf1d0ff09aa3ecddd651330a'
COMMIT='1438b1abfcaee3267092b75573014f4d9b4a983c'
def digest(raw):return hashlib.sha256(raw).hexdigest()
def verify(root):
 root=root.resolve();raw=(root/'package.json').read_bytes()
 assert digest(raw)==PIN,'Package revision differs'
 m=json.loads(raw)
 assert m['schema']=='forge.tidewater.fish-prop-package' and m['version']==1 and m['source_commit']==COMMIT
 files=m['files'];assert len(files)==250
 seen=set()
 def payload(name,pin):
  path=(root/name).resolve()
  assert path.is_relative_to(root) and name not in ('','.') and ':' not in name
  assert not (root/name).is_symlink()
  data=path.read_bytes()
  assert dict(bytes=len(data),sha256=digest(data))==pin
  seen.add(name);return data
 for name,pin in files.items():payload(name,pin)
 for key in ['props','material','license']:
  r=m[key];assert {k:v for k,v in r.items() if k!='file'}==files[r['file']]
 assert payload(m['license']['file'],files[m['license']['file']]).startswith(b'MIT License')
 assert {p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_file()}==seen|{'package.json'}
 props=json.loads((root/m['props']['file']).read_bytes())
 assert len(props['instances'])==78 and len(props['prototypes'])==18 and props['source_commit']==COMMIT
 refs=set()
 def refs_in(value):
  if isinstance(value,dict):
   if {'file','bytes','sha256'}<=value.keys():
    name='geometry/'+value['file'];assert files[name]==dict(bytes=value['bytes'],sha256=value['sha256']);refs.add(name)
   for child in value.values():refs_in(child)
  elif isinstance(value,list):
   for child in value:refs_in(child)
 refs_in(props)
 assert refs|{'geometry/fish-props.json'}=={n for n in files if n.startswith('geometry/')}
 material=json.loads((root/m['material']['file']).read_bytes())
 assert material['source_commit']==COMMIT and material['specular_mode']=='intended'
 assert {'material/'+n:p for n,p in material['files'].items()}=={n:p for n,p in files.items() if n.startswith('material/') and n!='material/fish-material.json'}
 return dict(package_sha256=digest(raw),payloads=len(files),instances=78,prototypes=18,rust_integrated=False,gpu_executed=False,visual_parity=False)
if __name__=='__main__':
 first,second,receipt=map(Path,sys.argv[1:]);assert first.resolve()!=second.resolve()
 a,b=verify(first),verify(second);assert a==b
 assert all(p.read_bytes()==(second/p.relative_to(first)).read_bytes() for p in first.rglob('*') if p.is_file())
 assert not receipt.exists();result=dict(schema='forge.tidewater.fish-prop-package-proof.v1',passed=True,independent_pair=True,**a)
 receipt.write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result))