from pathlib import Path
import json,hashlib,shutil,datetime
W=Path('F:/OpenRPG_ImportWork'); O=W/'house-physics-transport-v721'
assert not O.exists();O.mkdir();(O/'shapes').mkdir();(O/'acquisition-settings').mkdir()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
receipt=W/'house-runtime-state-glb-v675/RESULT.v1.json'; plan=W/'house-collider-binding-plan-v678/RESULT.v1.json'; mesh=W/'house-native-mesh-checkpoint-v685/RESULT.v1.json'; box=W/'house-native-box-checkpoint-v682/RESULT.v1.json'; candidate=W/'house-native-convex-candidate-v704/RESULT.v1.json'
files=[Path(__file__),receipt,plan,mesh,box,candidate];metrics={(r['source_guid'],r['source_file_id']):r for r in read(mesh)['rows']+read(box)['rows']};candidates={(r['source_guid'],r['source_file_id']):r for r in read(candidate)['rows']};rows=[];physical=empty=0
for carrier in read(receipt)['rows']:
 glb=Path(carrier['glb']);assert sha(glb)==carrier['sha256'];files.append(glb);components=[]
 for binding in [r for r in read(plan)['rows'] if r['prefab_guid']==carrier['prefab_guid']]:
  identity=binding['source_component']; key=(identity['guid'],identity['file_id']); entry={'source_binding':binding,'native_metrics':metrics.get(key),'backend_policy_qualified':False}
  if binding['kind']=='empty_mesh':
   assert key not in metrics;empty+=1;entry['shape_transport']=None
  else:
   assert key in metrics;physical+=1
   if key in candidates:
    c=candidates[key];src=Path(c['target_vertices']);assert sha(src)==c['target_vertices_sha256'];files.append(src);dst=O/'shapes'/(key[0]+'.'+key[1]+'.positions-f32.bin');shutil.copyfile(src,dst);assert sha(dst)==sha(src)
    entry['shape_transport']={'kind':'native_plane_candidate','relative_path':dst.relative_to(O).as_posix(),'sha256':sha(dst),'coordinate_space':'target_local','source_candidate_evidence':{'path':str(candidate),'sha256':sha(candidate)},'native_shape_equivalence':False}
   else:entry['shape_transport']={'kind':'source_triangles' if binding['kind']=='assigned_mesh' else 'native_box_observation','carrier_sha256':carrier['sha256']}
  components.append(entry)
 rows.append({'prefab_guid':carrier['prefab_guid'],'carrier':str(glb),'carrier_sha256':carrier['sha256'],'components':components})
assert len(rows)==24 and physical==22 and empty==2
project=W/'unity-library-source-thumbnail-house-bench-v2/project/ProjectSettings';settings=[]
for name in ['DynamicsManager.asset','TagManager.asset','TimeManager.asset','ProjectVersion.txt']:
 src=project/name;files.append(src);dst=O/'acquisition-settings'/name;shutil.copyfile(src,dst);settings.append({'relative_path':dst.relative_to(O).as_posix(),'sha256':sha(dst),'scope':'private_acquisition_project','original_pack_project_settings_proven':False})
pins={str(p):sha(p) for p in files}
manifest={'schema':'openrpg.source-physics-transport.v1','utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'rows':rows,'acquisition_settings':settings,'policy_limits':['Native measurements and source identities retained; no backend defaults supplied.','PhysX contactOffset is not admitted as Rapier ContactSkin without adapter evidence.','Static/dynamic friction, combine priorities and source layer filtering require adapter qualification.','Convex plane candidates retain the measured query residuals; no native equivalence claimed.'],'input_pins':pins,'studio_verified':False,'full_import_increment':0}
(O/'MANIFEST.v1.json').write_text(json.dumps(manifest,indent=2),'utf-8')
assert all(sha(p)==h for p,h in pins.items());result={'status':'physics_transport_prepared_verified','carriers':24,'physical_components':physical,'empty_observations':empty,'candidate_buffers':len(candidates),'manifest_sha256':sha(O/'MANIFEST.v1.json'),'unchanged_inputs':True,'full_import_increment':0,'backend_policy_qualified':False,'studio_verified':False};(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(result)
