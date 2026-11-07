"""Match captured collider ownership and mesh references to the actual embedded GLB buffers."""
from pathlib import Path
import datetime
import hashlib
import json
import math
import struct
from bind_native_scene_render_state_v1 import load_json

W=Path('F:/OpenRPG_ImportWork');O=W/'house-collider-binding-plan-v678'
assert not O.exists()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assembly_path=W/'house-runtime-state-glb-v675/RESULT.v1.json'
native_path=W/'house-native-component-state-v673/NATIVE-RESULT.v1.json'
assembly,native=map(read,[assembly_path,native_path])
pins={str(p):sha(p) for p in [assembly_path,native_path,Path(__file__)]}
native_cases={p['identity']['guid']:p for p in native['prefabs']}
rows=[];counts=dict(box=0,assigned_mesh=0,unassigned_mesh=0)
for scene in assembly['rows']:
    path=Path(scene['glb']);assert sha(path)==scene['sha256'];pins[str(path)]=scene['sha256']
    doc,offset,_=load_json(path)
    actual={n['node_path']:n for n in native_cases[scene['prefab_guid']]['nodes']}
    buffers=doc['extras']['preserved_collider_buffers']
    with path.open('rb') as stream:
        stream.seek(offset)
        size,tag=struct.unpack('<II',stream.read(8));assert tag==0x004e4942
        binary=stream.read(size);assert len(binary)==size
    def blob(desc):
        view=doc['bufferViews'][desc['buffer_view']]
        raw=binary[view.get('byteOffset',0):view.get('byteOffset',0)+view['byteLength']]
        assert hashlib.sha256(raw).hexdigest()==desc['sha256']
        return raw
    for index,node in enumerate(doc['nodes']):
        source=node['extras']['unity_source_node'];node_path=source['path']
        assert source['native_collider_observations']==actual[node_path]['colliders']
        for collider in source['native_collider_observations']:
            properties={p['property_path']:p for p in collider['serialized']}
            assert len(properties)==len(collider['serialized'])
            owner=properties['m_GameObject']['reference']
            assert (owner['guid'],owner['file_id'])==(source['gameobject']['guid'],source['gameobject']['file_id'])
            flags={field:properties[field]['boolean'] for field in ['m_Enabled','m_IsTrigger','m_ProvidesContacts']}
            assert all(properties[field]['property_type']=='Boolean' for field in flags)
            row=dict(prefab_guid=scene['prefab_guid'],card_ids=scene['cards'],glb=str(path),glb_sha256=scene['sha256'],node_index=index,node_path=node_path,source_component=collider['identity'],source_flags=flags,source_serialized_properties=collider['serialized'],native_world_matrix=node['extras']['target_world_matrix'],runtime_physics_admitted=False)
            if collider['identity']['type']=='UnityEngine.BoxCollider':
                center=properties['m_Center']['vector'];size=properties['m_Size']['vector']
                assert len(center)==len(size)==3 and all(math.isfinite(v) for v in center+size) and all(v>0 for v in size)
                row.update(kind='box',target_local_center=[center[0],center[1],-center[2]],target_local_half_extents=[v*.5 for v in size])
                counts['box']+=1
            else:
                assert collider['identity']['type']=='UnityEngine.MeshCollider'
                reference=properties['m_Mesh']['reference']
                row['source_mesh_reference']=reference
                row['source_convex']=properties['m_Convex']['boolean']
                row['source_cooking_options']=properties['m_CookingOptions']['number']
                if reference and reference['guid']:
                    matches=[b for b in buffers if b['node_path']==node_path and b['identity']['guid']==reference['guid'] and b['identity']['file_id']==reference['file_id']]
                    assert len(matches)==1
                    geometry=matches[0]
                    position=blob(geometry['attributes']['POSITION']);assert len(position)%12==0
                    vertex_count=len(position)//12
                    assert all(math.isfinite(v[0]) for v in struct.iter_unpack('<f',position))
                    triangles=0
                    assert len(geometry['index_buffers'])==len(geometry['primitives'])
                    for descriptor,primitive in zip(geometry['index_buffers'],geometry['primitives']):
                        raw=blob(descriptor);assert len(raw)==primitive['indices']*4
                        assert all(v[0]<vertex_count for v in struct.iter_unpack('<I',raw))
                        assert primitive['indices']%3==0;triangles+=primitive['indices']//3
                    row.update(kind='assigned_mesh',embedded_geometry=geometry,vertices=vertex_count,triangles=triangles)
                    counts['assigned_mesh']+=1
                else:
                    assert not any(b['node_path']==node_path for b in buffers)
                    row.update(kind='unassigned_mesh',collision_geometry_absent_in_source=True)
                    counts['unassigned_mesh']+=1
            rows.append(row)
assert len(rows)==24 and counts==dict(box=3,assigned_mesh=19,unassigned_mesh=2)
assert all(sha(p)==h for p,h in pins.items())
O.mkdir();result=dict(schema='hivemind.house-collider-binding-plan.v678',utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),colliders=24,counts=counts,rows=rows,input_pins=pins,rapier_runtime_qualified=False,physics_material_defaults_qualified=False,full_import=False)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps(dict(colliders=24,counts=counts,rapier_runtime_qualified=False)))
