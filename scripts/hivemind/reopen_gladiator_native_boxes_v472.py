"""Independently reopen SDK box queries against the immutable source geometry plan."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,math,struct,traceback
W=Path('F:/OpenRPG_ImportWork');I=W/'gladiator-native-box-metric-v471';O=W/'gladiator-native-box-reopen-v472'
assert not O.exists()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def f32(v):return struct.unpack('<f',struct.pack('<f',v))[0]
def bits(v):return struct.pack('<f',v)
def near(a,b):return math.isfinite(a) and math.isfinite(b) and abs(a-b)<=1e-6*max(1.,abs(b))
def vector_near(a,b):return len(a)==len(b) and all(near(x,y) for x,y in zip(a,b))
run=read(I/'RUN-RESULT.v1.json');assert run['status']=='three_native_box_query_sets_captured_review_pending' and run['unity_exit_code']==0
assert not run['source_changes'] and not run['private_prefab_changes']
native_path=I/'NATIVE-RESULT.v1.json';assert sha(native_path)==run['native_sha256']
prepared=read(I/'PREPARED.v1.json');plan_path=W/'gladiator-source-box-collider-plan-v467/RESULT.v1.json';plan=read(plan_path)
pins=dict(run['input_pins']);pins.update(plan['input_pins'])
for p,h in pins.items():assert sha(p)==h,p
for p in [Path(__file__),native_path,I/'RUN-RESULT.v1.json',I/'PREPARED.v1.json',plan_path]:pins[str(p)]=sha(p)
native=read(native_path);assert len(native['boxes'])==3 and native['original_components_unchanged'] and not native['purchased_scripts_executed'] and not native['physical_simulation_stepped']
identities={(r['source_identity']['guid'],r['source_identity']['file_id']):r for r in plan['rows']};rows=[];errors=[]
for box in native['boxes']:
 key=(box['source_guid'],box['source_file_id']);source=identities.pop(key);assert box['node_path']==source['node_path']
 assert list(map(bits,box['local_center']))==list(map(bits,source['source_center'])) and list(map(bits,box['local_size']))==list(map(bits,source['source_size']))
 assert box['enabled']==source['source_flags']['m_Enabled'] and box['is_trigger']==source['source_flags']['m_IsTrigger'] and box['provides_contacts']==source['source_flags']['m_ProvidesContacts']
 # These three authored root boxes have identity native poses. Other poses
 # require their composed transform comparator, never this identity shortcut.
 assert box['world_position']==[0.,0.,0.] and box['world_rotation']==[0.,0.,0.,1.] and box['lossy_scale']==[1.,1.,1.]
 center=box['local_center'];half=[f32(v*.5) for v in box['local_size']];point_errors=[];ray_errors=[]
 assert len(box['points'])==21 and len(box['rays'])==6
 for point in box['points']:
  axis=point['axis'];factor=point['factor'];expected_query=center.copy();expected_query[axis]=f32(center[axis]+f32(half[axis]*factor));assert vector_near(point['local_query'],expected_query) and vector_near(point['world_query'],expected_query)
  clipped=[min(max(v,f32(c-h)),f32(c+h)) for v,c,h in zip(expected_query,center,half)]
  error=max(abs(a-b) for a,b in zip(point['closest_world'],clipped));point_errors.append(error)
  if not vector_near(point['closest_world'],clipped):errors.append(dict(identity=key,kind='closest_point',actual=point['closest_world'],expected=clipped))
 for ray in box['rays']:
  axis=ray['axis'];sign=ray['sign'];expected_point=center.copy();expected_point[axis]=f32(center[axis]+f32(half[axis]*sign));normal=[0.,0.,0.];normal[axis]=float(sign)
  error=abs(ray['distance']-half[axis]);ray_errors.append(error)
  if not ray['hit'] or not near(ray['distance'],half[axis]) or not vector_near(ray['point'],expected_point) or not vector_near(ray['normal'],normal):errors.append(dict(identity=key,kind='ray',actual=ray,expected_point=expected_point,expected_normal=normal,expected_distance=half[axis]))
 material=box['implicit_material'];assert material['original_reference_null'] and source['source_physics_material_reference']['file_id']==''
 assert all(math.isfinite(material[k]) and 0<=material[k]<=1 for k in ['dynamic_friction','static_friction','bounciness'])
 assert math.isfinite(box['contact_offset']) and box['contact_offset']>0
 rows.append(dict(source_identity=source['source_identity'],closest_point_count=21,ray_count=6,max_closest_point_absolute_error=max(point_errors),max_ray_distance_absolute_error=max(ray_errors),measured_bench_implicit_material=material,measured_contact_offset=box['contact_offset'],source_material_reference_still_null=True,game_object_layer=box['game_object_layer'],game_object_tag=box['game_object_tag'],disabled_direct_raycast_hit_observed=box['disabled_raycast_hit'],disabled_closest_returns_query_observed=vector_near(box['disabled_closest'],box['disabled_query']),rapier_runtime_qualified=False))
assert not identities
for p,h in pins.items():assert sha(p)==h,p
O.mkdir();result=dict(status='three_native_box_query_sets_reopened' if not errors else 'native_box_query_differences_retained',observed_utc=datetime.now(timezone.utc).isoformat(),input_pins=pins,rows=rows,comparison_errors=errors,closest_points=63,rays=18,tolerance_relative_or_absolute=1e-6,defaults_observed_in_private_bench_not_authored_materials=True,rapier_runtime_qualified=False,studio_gui_verified=False,full_import=False,complete_asset_conversion_increment=0)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps(dict(status=result['status'],closest_points=63,rays=18,errors=len(errors))))
