"""Fold building material parameters and prove triangle membership in the acquired scene."""
from pathlib import Path
from collections import Counter
import hashlib,json,sys
import numpy as np
source,parts_root,output=map(Path,sys.argv[1:])
assert not output.exists()
parts=json.loads((parts_root/'buildings.json').read_text())
assert parts['schema']=='forge.tidewater.building-parts' and parts['version'] in [2,3]
render=json.loads((source/'village/render.json').read_text())
mapping={'wood':'wood','glass':'wood','hard':'hard','rope':'hard','roofMetal':'roofMetal',
         'thatch':'thatch','stone':'stone','fabric':'fabric','cloth':'fabric','flag':'fabric','net':'nets'}
names={'pos':'position','nrm':'normal','uv':'uv','tint':'tint','data':'vdata'}
widths={'pos':3,'nrm':3,'uv':2,'tint':3,'data':4}
def read(root,desc,dtype,key):
    file=root/desc[key]
    assert file.resolve().is_relative_to(root.resolve())
    raw=file.read_bytes()
    assert len(raw)==desc['bytes'] and hashlib.sha256(raw).hexdigest()==desc['sha256']
    return np.frombuffer(raw,dtype)
available={}; material_ids={}
for target in sorted(set(mapping.values())):
    node=next(n for n in render['nodes'] if n['name']=='village_'+target)
    assert node['matrix_world']==[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]
    g=render['geometries'][node['geometry']]
    vertices=np.concatenate([read(source,g['attributes'][names[n]],'<f4','path').reshape(-1,w) for n,w in widths.items()],axis=1)
    indices=read(source,g['indices'],'<u4','path').reshape(-1,3)
    available[target]=Counter(row.tobytes() for row in vertices[indices])
    material_ids[target]=node['material']
initial=sum(sum(c.values()) for c in available.values())
output.mkdir()
def write(array):
    raw=array.tobytes(); h=hashlib.sha256(raw).hexdigest(); name=h+'.bin'
    if not (output/name).exists(): (output/name).write_bytes(raw)
    return {'file':name,'bytes':len(raw),'sha256':h}
buildings=[]; matched=0
for building in parts['buildings']:
    outparts=[]
    for key,p in building['parts'].items():
        target=mapping[key]
        attrs={n:read(parts_root,p['attributes'][n],'<f4','file').reshape(-1,w) for n,w in widths.items()}
        data=read(parts_root,p['source_vdata'],'<f8','file').reshape(-1,4)
        assert len(data)==p['vertices'] and np.isfinite(data).all()
        assert np.array_equal(data.astype('<f4'),attrs['data'])
        folded=data.copy()
        if key=='glass': folded[:,2]=9; folded[:,3]=data[:,2]
        elif key=='rope': folded[:,1:3]=0; folded[:,3]=2+data[:,1]
        elif key=='cloth': folded[:,2:]=0
        elif key=='net': folded[:,2]=np.maximum(.02,data[:,2]); folded[:,3]=0
        elif key=='flag': folded[:,2]=data[:,2]+10000
        attrs['data']=folded.astype('<f4')
        indices=read(parts_root,p['indices'],'<u4','file').reshape(-1,3)
        assert indices.size and indices.max()<p['vertices']
        vertices=np.concatenate(list(attrs.values()),axis=1)
        for triangle in vertices[indices]:
            signature=triangle.tobytes()
            assert available[target][signature]>0, f"unmatched source triangle: {building['source_id']} {key}"
            available[target][signature]-=1; matched+=1
        outparts.append({'source_material':key,'material':material_ids[target],'target':target,
                         'vertices':p['vertices'],'attributes':{n:write(v) for n,v in attrs.items()},'indices':write(indices)})
    buildings.append({k:building[k] for k in ['source_id','kind','spec','result']}|{'parts':outparts})
manifest={'schema':'forge.tidewater.building-materials','version':1,'source_commit':parts['source_commit'],
          'buildings':buildings,'matched_source_triangles':matched,'remaining_scene_triangles':initial-matched,
          'proof':'Every triangle has identical ordered position, normal, UV, tint and remapped shader data in the acquired source material.',
          'limits':['world-space pivots not normalized','unclaimed scene geometry remains to partition','not yet a native split draw or Studio catalog']}
(output/'buildings.json').write_text(json.dumps(manifest,indent=2))
print(json.dumps({'buildings':len(buildings),'matched_source_triangles':matched,'remaining_scene_triangles':initial-matched}))
