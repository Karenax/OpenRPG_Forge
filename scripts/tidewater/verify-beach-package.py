"""Independently reopen portable yard assets and restore their source geometry/colliders."""
from pathlib import Path
import hashlib
import json
import sys
import numpy as np

first, second, raw_root, materials_root, result_path = map(Path, sys.argv[1:])
assert not result_path.exists()
sha=lambda b:hashlib.sha256(b).hexdigest()
files=sorted(p.name for p in first.iterdir())
assert files==sorted(p.name for p in second.iterdir())
assert all((first/n).read_bytes()==(second/n).read_bytes() for n in files)
manifest_raw=(first/'package.json').read_bytes()
manifest=json.loads(manifest_raw)
assert manifest['schema']=='forge.tidewater.beach-package' and manifest['version']==1
assert set(files)==set(manifest['files'])|{'package.json'}
for name,pin in manifest['files'].items():
    assert Path(name).name==name and '\\' not in name
    data=(first/name).read_bytes()
    assert len(data)==pin['bytes'] and sha(data)==pin['sha256']
materials_bytes=(materials_root/'materials.json').read_bytes()
assert sha(materials_bytes)==manifest['building_material_library_sha256']
materials=json.loads(materials_bytes)['materials']
refs=set()

def read(reference,dtype=None):
    assert reference['file'] in manifest['files']
    refs.add(reference['file'])
    assert {k:reference[k] for k in ('bytes','sha256')}==manifest['files'][reference['file']]
    data=(first/reference['file']).read_bytes()
    if dtype:
        assert reference['file']==reference['sha256']+'.bin'
        return np.frombuffer(data,dtype)
    return data

description=json.loads(read(manifest['props']))
assert b'MIT License' in read(manifest['license'])
raw_bytes=(raw_root/'beach-props.json').read_bytes()
raw=json.loads(raw_bytes)
assert description['source_descriptor_sha256']==sha(raw_bytes)
assert description['source_commit']==manifest['source_commit']==raw['source_commit']
assert len(description['props'])==43
triangles=0;max_error=0
for ordinal,obj in enumerate(description['props']):
    original=raw['props'][ordinal]
    assert obj['source_id']==original['source_id'] and obj['kind']==original['kind']
    assert obj['source_args']==original['args']
    frame=obj['source_frame'];anchor=np.array(frame['translation_m'],dtype=np.float64)
    assert anchor.shape==(3,) and np.isfinite(anchor).all()
    assert frame['rotation_xyzw']==[0,0,0,1] and frame['scale']==[1,1,1]
    points=[]
    for part in obj['parts']:
        assert part['material'] in materials
        p=read(part['attributes']['pos'],'<f4').reshape(-1,3)
        n=read(part['attributes']['nrm'],'<f4').reshape(-1,3)
        reference_p=read(part['reference_position'],'<f4').reshape(-1,3)
        reference_n=read(part['reference_normal'],'<f4').reshape(-1,3)
        assert np.array_equal(n,reference_n)
        error=float(np.max(np.abs(p.astype(np.float64)+anchor-reference_p)))
        assert error<.0001
        max_error=max(error,max_error)
        for name,width in [('pos',3),('nrm',3),('uv',2),('tint',3),('data',4)]:
            attr=read(part['attributes'][name],'<f4')
            assert len(attr)==part['vertices']*width and np.isfinite(attr).all()
        idx=read(part['indices'],'<u4')
        assert len(idx)>0 and len(idx)%3==0 and idx.max()<part['vertices']
        triangles+=len(idx)//3;points.append(p)
    points=np.concatenate(points)
    assert np.array_equal(points.min(axis=0),obj['local_bounds']['min'])
    assert np.array_equal(points.max(axis=0),obj['local_bounds']['max'])
    for kind in ('boxes','cylinders'):
        assert len(obj['colliders'][kind])==len(original['colliders'][kind])
        for local,world in zip(obj['colliders'][kind],original['colliders'][kind]):
            restored=dict(local)
            if kind=='boxes':restored['center']=(np.array(local['center'])+anchor).tolist()
            else:
                restored['x']+=anchor[0];restored['z']+=anchor[2]
                restored['yMin']+=anchor[1];restored['yMax']+=anchor[1]
            for key,value in world.items():
                if isinstance(value,(float,list)):
                    assert np.allclose(restored[key],value,rtol=0,atol=1e-12),(ordinal,kind,key)
                else:assert restored[key]==value
    assert not obj['lights'] and not original['lights']
assert refs==set(manifest['files']), 'Unreferenced payloads'
assert triangles==description['matched_source_triangles']==18807
result=dict(schema='forge.tidewater.beach-package-proof',version=1,status='passed',
            package_sha256=sha(manifest_raw),identical_files=len(files),props=43,
            triangles=triangles,reference_closure=True,collider_roundtrip=True,
            position_roundtrip_max_m=max_error,source_materials_preserved=True,
            studio_available=False,runtime_qualified=False,visual_parity=False)
result_path.write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result))
