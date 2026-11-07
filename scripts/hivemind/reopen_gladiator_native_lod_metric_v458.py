"""Reconcile all native camera LOD decisions with original source thresholds."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,math,struct
W=Path('F:/OpenRPG_ImportWork');I=W/'gladiator-native-lod-metric-v457';O=W/'gladiator-native-lod-metric-reopen-v458'
assert not O.exists()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def f32(v):return struct.unpack('<f',struct.pack('<f',v))[0]
def bits(v):return struct.pack('>f',v).hex().upper()
run=read(I/'RUN-RESULT.v1.json');assert run['status']=='five_prefab_native_camera_lod_metrics_captured_review_pending' and run['unity_exit_code']==0
assert not run['source_changes'] and not run['private_prefab_changes']
native=read(I/'NATIVE-RESULT.v1.json');assert sha(I/'NATIVE-RESULT.v1.json')==run['native_sha256']
assert native['captured_project_lod_bias']==1 and native['captured_project_maximum_lod_level']==0
bridge=read(W/'gladiator-source-binding-bridge-v390/RESULT.v1.json')
components=read(W/'gladiator-native-component-reopen-v451/RESULT.v1.json')
paths={r['prefab_guid']:r['native_component_state']['asset_path'] for r in components['rows']}
groups={paths[r['prefab_guid']]:r['lod_groups'][0] for r in bridge['rows']}
geometry={ (m['identity']['guid'],m['identity']['file_id']):m for rel in ['gladiator-converted-geometry-v396/RESULT.v1.json','gladiator-remaining-converted-geometry-v403/RESULT.v1.json'] for m in read(W/rel)['meshes'] }
expected=set()
for path,g in groups.items():
 assert g['fade_mode']==0
 heights=[f32(v) for v in [.01,.03,.1,.18,.25,.35,.6]]+[f32(f32(l['screen_relative_height'])*f32(factor)) for l in g['levels'] for factor in [.999,1.001]]
 for fov in [30.,60.,90.]:
  for h in heights:expected.add((path,fov,bits(h)))
assert len(expected)==len(native['samples'])==207
seen=set();distribution={};rows=[];counter_differences=[];max_relative_error=0.
for sample in native['samples']:
 key=(sample['prefab_path'],sample['field_of_view'],bits(sample['requested_relative_height']))
 assert key in expected and key not in seen;seen.add(key)
 group=groups[key[0]]
 assert sample['node_path']=='root' and sample['group_enabled']
 assert bits(sample['source_size'])==bits(group['size'])
 assert [bits(v) for v in sample['local_reference_point']]==[bits(group['reference_point'][a]) for a in 'xyz']
 fields={f['name']:f for f in sample['native_fields']}
 for field in fields.values():
  if field['type']=='System.Single':assert math.isfinite(field['float_value']) and bits(field['float_value'])==field['float_bits']
 height=fields['activeRelativeScreenSize']['float_value'];chosen=fields['activeLODLevel']['integer_value']
 wanted=next((i for i,l in enumerate(group['levels']) if height>f32(l['screen_relative_height'])),-1)
 assert chosen==wanted,(key,chosen,wanted)
 assert all(bits(height)!=bits(l['screen_relative_height']) for l in group['levels'])
 distance=math.dist(sample['camera_position'],sample['world_reference_point'])
 assert distance>0 and bits(sample['distance'])==sample['distance_bits']
 size=fields['worldSpaceSize']['float_value']
 assert abs(size-sample['source_size']*max(abs(v) for v in sample['lossy_scale']))<=1e-6*max(1.,size)
 predicted=size/(2*distance*math.tan(math.radians(sample['field_of_view'])/2))
 error=abs(predicted-height)/max(height,1e-12);max_relative_error=max(max_relative_error,error)
 assert error<=1e-6,(key,error)
 wanted_counts=dict(rendererCount=0,vertexCount=0,triangleCount=0,submeshCount=0)
 if chosen!=-1:
  renderers=group['levels'][chosen]['renderers'];wanted_counts['rendererCount']=len(renderers)
  for r in renderers:
   m=geometry[(r['mesh_guid'],r['mesh_file_id'])];wanted_counts['vertexCount']+=m['vertex_count'];wanted_counts['triangleCount']+=sum(p['triangles'] for p in m['primitives']);wanted_counts['submeshCount']+=len(m['primitives'])
 for name,count in wanted_counts.items():
  actual=fields[name]['integer_value']
  if actual!=count:counter_differences.append(dict(case=list(key),counter=name,native=actual,source_geometry=count))
 assert fields['rendererCount']['integer_value']==wanted_counts['rendererCount']
 distribution[str(chosen)]=distribution.get(str(chosen),0)+1
 rows.append(dict(prefab_path=key[0],field_of_view=key[1],requested_relative_height=sample['requested_relative_height'],native_relative_height=height,native_level=chosen,source_threshold_level=wanted,perspective_height_relative_error=error))
assert seen==expected
pins=dict(run['input_pins'])
for p in [Path(__file__),I/'RUN-RESULT.v1.json',I/'NATIVE-RESULT.v1.json',W/'gladiator-source-binding-bridge-v390/RESULT.v1.json',W/'gladiator-native-component-reopen-v451/RESULT.v1.json',W/'gladiator-converted-geometry-v396/RESULT.v1.json',W/'gladiator-remaining-converted-geometry-v403/RESULT.v1.json']:pins[str(p)]=sha(p)
for p,h in pins.items():assert sha(p)==h,p
O.mkdir();result=dict(status='207_native_gladiator_camera_lod_decisions_reconciled_with_source_thresholds',observed_utc=datetime.now(timezone.utc).isoformat(),input_pins=pins,rows=rows,case_count=207,distribution=distribution,max_perspective_height_relative_error=max_relative_error,native_geometry_counter_differences=counter_differences,actual_native_lod_selection_measured=True,forge_lod_driver_qualified=False,gpu_visibility_qualified=False,full_import=False,complete_asset_conversion_increment=0)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
note=f'''## Gladiator — décisions LOD Unity mesurées, 10 octobre 2026

207 mesures natives sur 5 prefabs, trois champs de vue et des distances autour des seuils LOD. Toutes les décisions correspondent aux seuils source ; le calcul de hauteur perspective concorde à une erreur relative maximale de {max_relative_error:.3g}. {len(counter_differences)} différences de compteurs géométriques natives sont conservées séparément.

Preuve : F:\\OpenRPG_ImportWork\\gladiator-native-lod-metric-reopen-v458\\RESULT.v1.json
Le host conserve actuellement un marqueur de driver LOD attendu ; aucun driver automatique n'est raccordé aux scènes importées. Prochaine étape : raccorder et vérifier le driver commun avec ces références natives. Colliders physiques, rendu comparé et Studio restent ouverts. Aucun nouvel asset complet qualifié.

''';(O/'LOT.md').write_text(note,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
 p=W/'SUIVI_LOTS_HIVEMIND'/name;before=p.read_bytes();(O/(name+'.before')).write_bytes(before);p.write_bytes(note.encode('utf-8')+before)
print(json.dumps(dict(status=result['status'],cases=207,distribution=distribution,counter_differences=len(counter_differences),max_relative_error=max_relative_error)))
