"""Derive source-pinned local box geometry; no physical runtime qualification."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,itertools,json,math,struct
W=Path('F:/OpenRPG_ImportWork');O=W/'gladiator-source-box-collider-plan-v467'
assert not O.exists()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def f32(v):return struct.unpack('<f',struct.pack('<f',v))[0]
def bits(v):return struct.pack('<f',v).hex()
def document(p):
 with Path(p).open('rb') as f:
  magic,version,length=struct.unpack('<III',f.read(12));assert magic==0x46546c67 and version==2
  size,kind=struct.unpack('<II',f.read(8));assert kind==0x4e4f534a
  return json.loads(f.read(size))
native_path=W/'gladiator-native-component-state-v447/NATIVE-RESULT.v1.json';assembly_path=W/'gladiator-runtime-state-glb-v452/RESULT.v1.json';verified_path=W/'gladiator-native-component-reopen-v451/RESULT.v1.json'
native=read(native_path);assembly=read(assembly_path);verified=read(verified_path)
assert verified['status']=='five_original_prefab_component_states_independently_reopened'
pins={str(p):sha(p) for p in [Path(__file__),native_path,assembly_path,verified_path]};rows=[]
by_guid={p['identity']['guid']:p for p in native['prefabs']}
for scene in assembly['rows']:
 assert sha(scene['glb'])==scene['sha256'];pins[scene['glb']]=scene['sha256'];doc=document(scene['glb'])
 for index,node in enumerate(doc['nodes']):
  source=node['extras']['unity_source_node'];observations=source['native_collider_observations']
  native_node=next(n for n in by_guid[scene['prefab_guid']]['nodes'] if n['node_path']==source['path'])
  assert observations==native_node['colliders']
  for collider in observations:
   identity=collider['identity'];assert identity['type']=='UnityEngine.BoxCollider' and identity['guid']==scene['prefab_guid']
   properties={p['property_path']:p for p in collider['serialized']};assert len(properties)==len(collider['serialized'])
   center=properties['m_Center']['vector'];size=properties['m_Size']['vector'];assert len(center)==len(size)==3
   assert all(math.isfinite(v) and f32(v)==v for v in center+size) and all(v>0 for v in size)
   owner=properties['m_GameObject']['reference'];assert owner['guid']==source['gameobject']['guid'] and owner['file_id']==source['gameobject']['file_id']
   target_center=[center[0],center[1],-center[2]];half=[f32(v*.5) for v in size]
   assert all(bits(f32(h*2))==bits(s) for h,s in zip(half,size))
   corners=[]
   for sign in itertools.product([-1,1],repeat=3):
    original=[f32(c+f32(h*s)) for c,h,s in zip(center,half,sign)]
    expected=[original[0],original[1],-original[2]]
    target_sign=[sign[0],sign[1],-sign[2]]
    derived=[f32(c+f32(h*s)) for c,h,s in zip(target_center,half,target_sign)]
    assert list(map(bits,expected))==list(map(bits,derived));corners.append(dict(source=original,target=derived))
   flags={key:properties[key]['boolean'] for key in ['m_Enabled','m_IsTrigger','m_ProvidesContacts']}
   for key in flags:assert properties[key]['property_type']=='Boolean'
   rows.append(dict(prefab_guid=scene['prefab_guid'],glb=scene['glb'],node_index=index,node_path=source['path'],source_identity=identity,source_center=center,source_size=size,target_local_center=target_center,target_local_half_extents=half,source_flags=flags,source_physics_material_reference=properties['m_Material']['reference'],source_layer_overrides={key:properties[key]['number'] for key in ['m_IncludeLayers.m_Bits','m_ExcludeLayers.m_Bits','m_LayerOverridePriority']},corners=corners,conversion_basis=[1,1,-1],placement_transform_must_remain_composed=True,physics_independent_of_render_lod=True,physical_material_defaults_qualified=False,contact_offset_qualified=False,rapier_runtime_qualified=False))
assert len(rows)==3
for p,h in pins.items():assert sha(p)==h,p
O.mkdir();result=dict(status='three_original_box_collider_local_geometries_derived_and_reopened',observed_utc=datetime.now(timezone.utc).isoformat(),input_pins=pins,rows=rows,colliders=3,corner_checks=24,source_box_flags_preserved=True,source_sources_modified=False,glbs_modified=False,rapier_runtime_qualified=False,studio_gui_verified=False,full_import=False,complete_asset_conversion_increment=0)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps(dict(status=result['status'],colliders=3,corner_checks=24,rapier_runtime_qualified=False)))
