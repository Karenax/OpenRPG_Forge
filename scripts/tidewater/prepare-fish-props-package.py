"""Prepare a portable FishProps source package after both independent audits."""
from pathlib import Path
import importlib.util,json,hashlib,subprocess,sys
source,geometry_a,geometry_b,material_a,material_b,out=map(lambda x:Path(x).resolve(),sys.argv[1:])
assert not out.exists()
assert not out.is_relative_to(source)
scripts=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('fish_props_audit',scripts/'verify-fish-props.py')
audit=importlib.util.module_from_spec(spec);spec.loader.exec_module(audit)
audit.verify(source,geometry_a,geometry_b)
material_receipt=out.with_name(out.name+'-material-validation.json')
assert not material_receipt.exists()
subprocess.run([sys.executable,str(scripts/'verify-fish-material.py'),str(material_a),str(material_b),str(source),str(material_receipt)],check=True)
sha=lambda b:hashlib.sha256(b).hexdigest()
inputs={}
for prefix,root in [('geometry',geometry_a),('material',material_a)]:
 for path in sorted(root.iterdir()):
  assert path.is_file() and not path.is_symlink()
  inputs[f'{prefix}/{path.name}']=path.read_bytes()
license_bytes=(source/'LICENSE').read_bytes()
assert license_bytes.startswith(b'MIT License')
inputs['LICENSE-Tidewater.txt']=license_bytes
files={name:dict(bytes=len(raw),sha256=sha(raw)) for name,raw in inputs.items()}
reference=lambda name:dict(file=name,**files[name])
manifest=dict(schema='forge.tidewater.fish-prop-package',version=1,source_commit=audit.COMMIT,props=reference('geometry/fish-props.json'),material=reference('material/fish-material.json'),license=reference('LICENSE-Tidewater.txt'),files=files,qualification='source-exact-package; Rust integration and GPU visual parity pending')
raw=(json.dumps(manifest,indent=2,sort_keys=True)+'\n').encode()
out.mkdir()
for name,data in inputs.items():
 destination=out/name;destination.parent.mkdir(exist_ok=True)
 destination.write_bytes(data)
 assert sha(destination.read_bytes())==files[name]['sha256']
(out/'package.json').write_bytes(raw)
assert {p.relative_to(out).as_posix() for p in out.rglob('*') if p.is_file()}==set(inputs)|{'package.json'}
assert all((geometry_a/name.removeprefix('geometry/')).read_bytes()==data for name,data in inputs.items() if name.startswith('geometry/'))
assert all((material_a/name.removeprefix('material/')).read_bytes()==data for name,data in inputs.items() if name.startswith('material/'))
print(json.dumps(dict(files=len(files),package_sha256=sha(raw),source_unchanged=True,rust_integrated=False,gpu_executed=False)))