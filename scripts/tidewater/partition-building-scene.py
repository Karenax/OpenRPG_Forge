"""Assign original triangle ranges using observed pre-assembly offsets, without spatial guessing."""
from pathlib import Path
import hashlib,json,sys
import numpy as np
source,parts_root,output=map(Path,sys.argv[1:]); assert not output.exists()
parts=json.loads((parts_root/'buildings.json').read_text()); assert parts['version']==3
render=json.loads((source/'village/render.json').read_text())
batches=parts['assembly_batches']
groups={'wood':['wood','glass'],'hard':['hard','rope'],'roofMetal':['roofMetal'],
        'thatch':['thatch'],'stone':['stone'],'fabric':['fabric','cloth','flag'],'nets':['net']}
offsets={}; base=0
for target,keys in groups.items():
    vp=ip=0
    for key in keys:
        offsets[key]=(target,(base if target not in ['fabric','nets'] else 0)+vp,ip)
        counts=batches.get(key,{'vertices':0,'indices':0}); vp+=counts['vertices'];ip+=counts['indices']
    if target not in ['fabric','nets']: base+=vp
def read(root,desc,key):
    file=root/desc[key]; assert file.resolve().is_relative_to(root.resolve())
    raw=file.read_bytes();assert len(raw)==desc['bytes'] and hashlib.sha256(raw).hexdigest()==desc['sha256']
    return np.frombuffer(raw,'<u4')
nodes={}; indices={}; owners={}
for target in groups:
    node=next(n for n in render['nodes'] if n['name']=='village_'+target)
    g=render['geometries'][node['geometry']]; idx=read(source,g['indices'],'path')
    assert len(idx)%3==0
    nodes[target]=node;indices[target]=idx;owners[target]=np.zeros(len(idx)//3,dtype='<u4')
assigned=0
for owner,b in enumerate(parts['buildings'],1):
    for key,part in b['parts'].items():
        target,vertex_base,index_base=offsets[key]
        local=read(parts_root,part['indices'],'file')
        start=index_base+part['start']['indices'];end=start+len(local)
        assert start%3==0 and end<=len(indices[target])
        expected=local.astype('uint64')+vertex_base+part['start']['vertices']
        assert np.array_equal(indices[target][start:end],expected),f'original index mismatch {owner} {key}'
        span=owners[target][start//3:end//3];assert not span.any(),'overlapping semantic ownership'
        span[:]=owner;assigned+=len(local)//3
output.mkdir();partition=[];total=0;run_count=0
for target,labels in owners.items():
    starts=np.r_[0,np.flatnonzero(labels[1:]!=labels[:-1])+1,len(labels)]
    runs=[{'owner':int(labels[a]),'first_index':int(a*3),'index_count':int((z-a)*3)} for a,z in zip(starts[:-1],starts[1:])]
    reconstructed=np.concatenate([indices[target][r['first_index']:r['first_index']+r['index_count']] for r in runs])
    assert reconstructed.tobytes()==indices[target].tobytes()
    raw=labels.tobytes();h=hashlib.sha256(raw).hexdigest();(output/(h+'.bin')).write_bytes(raw)
    partition.append({'source_node':nodes[target]['source_id'],'name':nodes[target]['name'],'geometry':nodes[target]['geometry'],
                      'material':nodes[target]['material'],'owners':{'file':h+'.bin','bytes':len(raw),'sha256':h},'runs':runs})
    total+=len(labels);run_count+=len(runs)
manifest={'schema':'forge.tidewater.village-partition','version':1,'source_commit':parts['source_commit'],
          'buildings':[b['source_id'] for b in parts['buildings']],'nodes':partition,'triangles':total,
          'building_triangles':assigned,'residual_triangles':total-assigned,'ordered_runs':run_count,
          'source_index_streams_exact':True,'semantic_offsets_exact':True,
          'limits':['world-space coordinates retained','native split rendering not yet qualified','other village nodes retained separately']}
(output/'partition.json').write_text(json.dumps(manifest,indent=2))
print(json.dumps({k:manifest[k] for k in ['triangles','building_triangles','residual_triangles','ordered_runs','source_index_streams_exact']}))
