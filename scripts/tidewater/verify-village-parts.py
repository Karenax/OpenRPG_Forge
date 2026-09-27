"""Qualify repeatability, triangle ownership and finite building payloads."""
from pathlib import Path
import hashlib,json,sys
import numpy as np
first,second,output=map(Path,sys.argv[1:])
assert not output.exists()
files=sorted(p.name for p in first.iterdir())
assert files==sorted(p.name for p in second.iterdir())
assert all((first/n).read_bytes()==(second/n).read_bytes() for n in files)
data=json.loads((first/'buildings.json').read_text())
assert data['unchanged_merged_scene'] and data['source_commit']=='1438b1abfcaee3267092b75573014f4d9b4a983c'
ids=set(); ranges={}; rows=[]
def read(desc,dtype):
    assert Path(desc['file']).name==desc['file']
    raw=(first/desc['file']).read_bytes()
    assert len(raw)==desc['bytes'] and hashlib.sha256(raw).hexdigest()==desc['sha256']
    return np.frombuffer(raw,dtype)
for b in data['buildings']:
    assert b['source_id'] not in ids
    ids.add(b['source_id']); vertices=triangles=0; positions=[]
    for material,part in b['parts'].items():
        if data['version']>=2:
            original=read(part['source_vdata'],'<f8')
            assert len(original)==part['vertices']*4 and np.isfinite(original).all()
            assert np.array_equal(original.astype('<f4'),read(part['attributes']['data'],'<f4'))
        for name,width in [('pos',3),('nrm',3),('uv',2),('tint',3),('data',4)]:
            values=read(part['attributes'][name],'<f4')
            assert len(values)==part['vertices']*width and np.isfinite(values).all()
            if name=='pos': positions.append(values.reshape(-1,3))
        idx=read(part['indices'],'<u4')
        assert len(idx)%3==0 and len(idx)>0 and int(idx.max())<part['vertices']
        start=part['start']['vertices']; end=start+part['vertices']
        assert all(end<=a or start>=z for a,z in ranges.get(material,[])), 'building vertex ranges overlap'
        ranges.setdefault(material,[]).append((start,end))
        vertices+=part['vertices'];triangles+=len(idx)//3
    pos=np.concatenate(positions)
    rows.append({'id':b['source_id'],'kind':b['kind'],'name':b['spec'].get('name'),
                 'vertices':vertices,'triangles':triangles,'bounds':[pos.min(axis=0).tolist(),pos.max(axis=0).tolist()]})
report={'files_exact':len(files),'buildings':len(rows),'parts':sum(len(b['parts']) for b in data['buildings']),
        'vertices':sum(b['vertices'] for b in rows),'triangles':sum(b['triangles'] for b in rows),
        'material_keys':sorted(ranges),'all_payloads_finite':True,'local_indices_valid':True,'ranges_disjoint':True,
        'unchanged_merged_scene_digest':data['scene_digest'],'inventory':rows,
        'limits':['pre-folding material keys','world coordinates retained','not catalog or Studio qualification']}
output.write_text(json.dumps(report,indent=2));print(json.dumps({k:v for k,v in report.items() if k!='inventory'},indent=2))
