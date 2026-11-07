"""Reopen the exact-LOD comparisons without qualifying any Forge conversion."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');CAP=W/'pirate-wood-lod-migration-v591';O=W/'pirate-wood-lod-migration-review-v592'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
run=read(CAP/'RUN-RESULT.v1.json');prep=read(CAP/'PREPARED.v1.json')
assert run['status']=='pirate_migration_native_comparison_completed' and run['unity_exit_code']==0
assert not run['input_changes'] and run['all_views_repeat_exact'] and run['all_views_migration_exact']
assert len(prep['cases'])==144 and len(run['comparisons'])==72
pins=prep['input_pins']|run['output_pins']
# The historical proof explains the entire serialized wood diff; only its
# material, native-default receipt and original snapshot are relevant here.
priorpath=W/'pirate-wood-defaults-reopen-v208/RESULT.v1.json';prior=read(priorpath)
assert prior['textures_keywords_and_other_serialized_values_unchanged'] and prior['stencil_references_unchanged']
assert prior['material_guid']=='abc00000000004592200770135964921'
for name,h in prior['input_pins'].items():
    if Path(name).name in {'MATERIAL-NATIVE.v1.json','M_Wood_Planks_02.mat','verify_pirate_wood_defaults_v208.py'}:
        assert sha(name)==h,name;pins[name]=h
rows=[]
for i in range(36):
    requests=prep['cases'][i*4:(i+1)*4]
    receipts=[]
    for request in requests:
        p=Path(request['output']);native=read(p);pins[str(p)]=sha(p)
        assert native['id']==request['id'] and native['prefab_guid']==request['prefab_guid']
        assert native['requested_lod']==request['requested_lod'] and native['requested_lod_selection_verified']
        assert native['source_object_unchanged'] and native['preview_scene_isolated']
        assert native['source_empty_lod_slots']==request['expected_empty_lod_slots']
        assert native['gpu']=='Direct3D11' and len(native['images'])==2
        assert native['selected_mesh_identities'] and all(v>0 for v in native['selected_mesh_vertex_counts'])
        assert all(p['compiled'] for p in native['compiled_passes'])
        receipts.append(native)
    for key in ['selected_mesh_identities','selected_mesh_vertex_counts','disabled_lod_mesh_identities','renderers','textures']:
        assert all(r[key]==receipts[0][key] for r in receipts),key
    for view in range(2):
        raw=[Path(r['images'][view]['raw_path']).read_bytes() for r in receipts[1:]]
        assert len(raw[0])==512*512*8 and raw[0]==raw[1]==raw[2]
    rows.append(dict(source_card_id=requests[0]['source_card_id'],source_path=requests[0]['prefab_path'],requested_lod=requests[0]['requested_lod'],selected_mesh_identities=receipts[0]['selected_mesh_identities'],selected_mesh_vertex_counts=receipts[0]['selected_mesh_vertex_counts'],views_compared=2,raw_repeat_exact=True,raw_stencil_migration_exact=True))
for start in range(0,36,3):
    controls=rows[start:start+3];assert [r['requested_lod'] for r in controls]==[0,1,2]
    assert all(r['selected_mesh_identities']!=controls[0]['selected_mesh_identities'] for r in controls[1:])
for p in [Path(__file__),Path(__file__).with_name('run_pirate_wood_lod_migration_v590.py'),Path(__file__).with_name('run_pirate_wood_lod_migration_v591.py'),CAP/'RUN-RESULT.v1.json',CAP/'PREPARED.v1.json',priorpath]:pins[str(p)]=sha(p)
for p,h in pins.items():assert sha(p)==h,p
result=dict(status='twelve_wood_groups_three_exact_lods_native_stencil_comparisons_verified',groups_verified=12,lod_cases_verified=36,view_comparisons_verified=72,rows=rows,input_pins=pins,source_inputs_unchanged=True,source_image_migration_scope_verified=True,original_sdk_pipeline_equivalence=False,forge_geometry_conversion_verified=False,actual_studio_gui_verified=False,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat())
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in {'rows','input_pins'}}))
