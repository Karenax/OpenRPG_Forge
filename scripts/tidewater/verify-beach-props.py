"""Verify repeatable per-object acquisition against the original merged buffers."""
from pathlib import Path
from collections import Counter
import hashlib
import json
import sys
import numpy as np

first, second, acquisition, parts_root, output = map(Path, sys.argv[1:])
assert not output.exists(), 'Fresh result required'
sha = lambda raw: hashlib.sha256(raw).hexdigest()
files = sorted(p.name for p in first.iterdir())
assert files == sorted(p.name for p in second.iterdir())
assert all((first/name).read_bytes() == (second/name).read_bytes() for name in files)
props = json.loads((first/'beach-props.json').read_bytes())
buildings = json.loads((parts_root/'buildings.json').read_bytes())
render = json.loads((acquisition/'village/render.json').read_bytes())
assert props['source_commit'] == buildings['source_commit']
assert props['source_file_sha256'] == buildings['source_file_sha256']
assert props['unchanged_merged_scene'] and props['unchanged_terrain_and_colliders']
prior_digest='e2f4799bb6f425afb4b9b800fb547528a037da15ad95f8c9d2182889c1e805f1'
scene_digest_matches_prior=props['source_scene_digest']==prior_digest
counts = Counter(p['kind'] for p in props['props'])
assert counts == dict(rowboat=4,wreck=1,oar=2,netRack=3,fishRack=1,barrel=5,crate=8,trap=11,buoy=5,ropeCoil=2,buoyFence=1), counts

def read(root, desc, dtype, key='file'):
    location = (root/desc[key]).resolve()
    assert location.is_relative_to(root.resolve())
    raw = location.read_bytes()
    assert len(raw) == desc['bytes'] and sha(raw) == desc['sha256']
    return np.frombuffer(raw, dtype)

groups = dict(wood=['wood','glass'],hard=['hard','rope'],roofMetal=['roofMetal'],
              thatch=['thatch'],stone=['stone'],fabric=['fabric','cloth','flag'],nets=['net'])
offsets,base = {},0
for target,keys in groups.items():
    vertices = indices = 0
    for key in keys:
        offsets[key]=(target,(base if target not in ('fabric','nets') else 0)+vertices,indices)
        batch=buildings['assembly_batches'].get(key,dict(vertices=0,indices=0))
        vertices+=batch['vertices'];indices+=batch['indices']
    if target not in ('fabric','nets'):base+=vertices
geometries={target:render['geometries'][next(n for n in render['nodes'] if n['name']==f'village_{target}')['geometry']] for target in groups}
ranges={target:[] for target in groups}
for obj in buildings['buildings']:
    for key,p in obj['parts'].items():
        target,_,ib=offsets[key];start=ib+p['start']['indices']
        ranges[target].append((start,start+p['indices']['bytes']//4,obj['source_id']))
yard=json.loads((first.parent/'village-parts-20261004-v1/village-props.json').read_bytes())
for obj in yard['props']:
    for key,p in obj['parts'].items():
        target,_,ib=offsets[key];start=ib+p['start']['indices']
        ranges[target].append((start,start+p['indices']['bytes']//4,obj['source_id']))
inventory=[]
for ordinal,obj in enumerate(props['props']):
    assert obj['source_id']==f"tidewater:{props['source_commit']}:beach-prop:{ordinal}"
    triangles=0
    for key,p in obj['parts'].items():
        target,vb,ib=offsets[key];geometry=geometries[target]
        idx=read(first,p['indices'],'<u4')
        assert len(idx)>0 and len(idx)%3==0 and idx.max()<p['vertices']
        start=ib+p['start']['indices'];end=start+len(idx)
        assert np.array_equal(read(acquisition,geometry['indices'],'<u4','path')[start:end],idx.astype('<u8')+vb+p['start']['vertices'])
        for s,e,owner in ranges[target]:assert end<=s or start>=e,(obj['source_id'],owner)
        ranges[target].append((start,end,obj['source_id']))
        for name,source_name,width in [('pos','position',3),('nrm','normal',3),('uv','uv',2),('tint','tint',3),('data','vdata',4)]:
            attr=read(first,p['attributes'][name],'<f4')
            assert len(attr)==p['vertices']*width and np.isfinite(attr).all()
            if name=='data':
                # Village._assemble folds emitter materials in JavaScript f64
                # before uploading Float32. Compare the actual folded result.
                original_data=read(first,p['source_vdata'],'<f8').reshape(-1,4)
                folded=original_data.copy()
                if key=='glass':folded[:,2]=9;folded[:,3]=original_data[:,2]
                elif key=='rope':folded[:,1:3]=0;folded[:,3]=2+original_data[:,1]
                elif key=='cloth':folded[:,2:4]=0
                elif key=='net':folded[:,2]=np.maximum(.02,original_data[:,2]);folded[:,3]=0
                elif key=='flag':folded[:,2]=original_data[:,2]+10000
                attr=folded.astype('<f4').reshape(-1)
            source_attr=read(acquisition,geometry['attributes'][source_name],'<f4','path')
            vs=(vb+p['start']['vertices'])*width
            assert np.array_equal(attr,source_attr[vs:vs+len(attr)]),(ordinal,key,name)
        assert np.array_equal(read(first,p['source_vdata'],'<f8').astype('<f4'),read(first,p['attributes']['data'],'<f4'))
        triangles+=len(idx)//3
    assert triangles>0
    inventory.append(dict(source_id=obj['source_id'],kind=obj['kind'],triangles=triangles,
                          collider_boxes=len(obj['colliders']['boxes']),collider_cylinders=len(obj['colliders']['cylinders'])))
result=dict(schema='forge.tidewater.beach-parts-proof',version=1,status='geometry_verified_scene_drift_unresolved' if not scene_digest_matches_prior else 'passed',
            source_commit=props['source_commit'],identical_files=len(files),
            source_scene_digest=props['source_scene_digest'],prior_scene_digest=prior_digest,scene_digest_matches_prior=scene_digest_matches_prior,full_scene_qualified=False,source_attributes_and_index_ranges_exact=True,
            building_ranges_disjoint=True,object_ranges_disjoint=True,inventory=inventory,
            kind_counts=dict(counts),total_triangles=sum(p['triangles'] for p in inventory),
            limits=['Raw acquisition; no portable asset or Studio rendering verdict',
                    'FishProps instance content not covered'] +
                   ([] if scene_digest_matches_prior else
                    ['Global scene digest differs from prior acquisition; qualification remains open']))
output.write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='inventory'}))
raise SystemExit(0 if scene_digest_matches_prior else 2)
