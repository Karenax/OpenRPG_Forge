"""Create compact per-run native meshes while preserving original indexed attributes."""
from pathlib import Path
import hashlib,json,os,sys
import numpy as np
scene,partition_root,output=map(Path,sys.argv[1:]);assert not output.exists()
hash=lambda b:hashlib.sha256(b).hexdigest()
fixture=json.loads((scene/'fixture.json').read_text())
partition_raw=(partition_root/'partition.json').read_bytes();partition=json.loads(partition_raw)
assert partition['schema']=='forge.tidewater.village-partition' and partition['source_index_streams_exact']
village=json.loads((scene/'village.json').read_text());assert village['version']==1
output.mkdir()
for name,digest in fixture['files'].items():
    assert Path(name).name==name and hash((scene/name).read_bytes())==digest
    if name!='village.json':os.link(scene/name,output/name)
def save(raw):
    digest=hash(raw);name='village-split-'+digest+'.bin'
    if not (output/name).exists():(output/name).write_bytes(raw)
    fixture['files'][name]=digest
    return name
nodes=[];geometries={};vertices_total=0
by_id={n['source_node']:n for n in partition['nodes']}
for node in village['nodes']:
    original=village['geometries'][node['geometry']]
    group=by_id.get(node['source_id'])
    if group is None:
        nodes.append(node);geometries[node['geometry']]=original;continue
    assert group['geometry']==node['geometry']
    idx=np.frombuffer((scene/original['indices']).read_bytes(),'<u4')
    owners_raw=(partition_root/group['owners']['file']).read_bytes()
    assert hash(owners_raw)==group['owners']['sha256'] and len(owners_raw)==group['owners']['bytes']
    owners=np.frombuffer(owners_raw,'<u4'); assert len(owners)*3==len(idx)
    cursor=0
    for ordinal,run in enumerate(group['runs']):
        start=run['first_index'];end=start+run['index_count']; assert start==cursor and end<=len(idx)
        assert (owners[start//3:end//3]==run['owner']).all()
        selected=idx[start:end]; used,inverse=np.unique(selected,return_inverse=True)
        assert np.array_equal(used[inverse],selected)
        attrs={}
        for name,width in [('position',3),('normal',3),('uv',2),('tint',3),('vdata',4)]:
            values=np.frombuffer((scene/original['attributes'][name]).read_bytes(),'<f4').reshape(-1,width)
            compact=values[used]
            assert compact[inverse].tobytes()==values[selected].tobytes()
            attrs[name]=save(compact.tobytes())
        descriptor={'attributes':attrs,'indices':save(inverse.astype('<u4').tobytes())}
        geometry=hash(json.dumps(descriptor,sort_keys=True).encode());geometries[geometry]=descriptor
        owner=run['owner'];assert 0<=owner<=len(partition['buildings'])
        nodes.append(node|{'source_id':node['source_id']+f'/run/{ordinal}','geometry':geometry,
                           'building_source_id':partition['buildings'][owner-1] if owner else None})
        cursor=end;vertices_total+=len(used)
    assert cursor==len(idx)
village.update(version=2,nodes=nodes,geometries=geometries,partition_sha256=hash(partition_raw))
raw=json.dumps(village,indent=2).encode();(output/'village.json').write_bytes(raw)
fixture['files']['village.json']=hash(raw)
(output/'fixture.json').write_text(json.dumps(fixture,indent=2))
print(json.dumps({'nodes':len(nodes),'geometries':len(geometries),'compact_static_vertices':vertices_total,'partition_sha256':hash(partition_raw)}))
