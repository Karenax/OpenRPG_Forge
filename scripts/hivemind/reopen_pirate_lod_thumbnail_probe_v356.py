"""Validate native LOD selection identities and reopen the six variant thumbnails."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
from PIL import Image,ImageDraw
W=Path('F:/OpenRPG_ImportWork');CAP=W/'pirate-lod-thumbnail-probe-v354';O=W/'pirate-lod-thumbnail-review-v356'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
guardpath=W/'pirate-lod-thumbnail-probe-guard-v355/RESULT.v1.json';guard=read(guardpath)
prep=read(CAP/'PREPARED.v1.json');batch=read(CAP/'NATIVE-RESULT.v1.json');run=read(CAP/'RUN-RESULT.v1.json')
assert run['unity_exit_code']==0 and run['finished_utc'] and not run['input_changes']
assert len(guard['admitted_ids'])==len(batch['cases'])==9 and not guard['cases_refused_source_parity']
pins=guard['input_pins'];rows=[];controls={};comparisons=[]
for request,case in zip(prep['cases'],batch['cases']):
    assert case['id']==request['id'] and case['captured']
    native=read(case['receipt'])
    assert native['id']==request['id'] and native['prefab_guid']==request['prefab_guid']
    assert native['requested_lod']==request['requested_lod'] and native['requested_lod_selection_verified']
    assert not native['lod0_for_thumbnail_only']
    assert native['source_lod_groups']>0 and native['source_empty_lod_slots']==request['expected_empty_lod_slots']
    assert native['source_object_unchanged'] and native['preview_scene_isolated']
    assert native['presentation_only'] and native['presentation_fixed_ev']==8 and native['gpu']=='Direct3D11'
    source_meshes={r['mesh_guid']+':'+r['mesh_file_id'] for r in native['renderers']}
    assert native['selected_mesh_identities'] and set(native['selected_mesh_identities'])<=source_meshes
    assert len(native['selected_mesh_identities'])==len(native['selected_mesh_vertex_counts'])
    assert all(n>0 for n in native['selected_mesh_vertex_counts'])
    assert native['compiled_passes'] and all(p['compiled'] for p in native['compiled_passes'])
    assert len(native['images'])==2
    for image in native['images']:
        p=Path(image['path']);assert sha(p)==image['sha256'];pins[str(p)]=image['sha256']
        assert image['clipped_rgb_pixels']==0 and image['linear_max_channel']<=1
        with Image.open(p) as png:
            png.load();assert png.format=='PNG' and png.size==(512,512) and png.convert('RGB').getcolors(1) is None
    pins[case['receipt']]=sha(case['receipt'])
    if not request['index_candidate']:
        assert request['requested_lod']==0;controls[request['prefab_guid']]=native;continue
    control=controls[request['prefab_guid']]
    comparison=dict(id=request['id'],requested_lod=request['requested_lod'],selected_mesh_identities=native['selected_mesh_identities'],lod0_mesh_identities=control['selected_mesh_identities'],requested_vertices=sum(native['selected_mesh_vertex_counts']),lod0_vertices=sum(control['selected_mesh_vertex_counts']),mesh_selection_differs_from_lod0=native['selected_mesh_identities']!=control['selected_mesh_identities'],source_empty_lod_slots=native['source_empty_lod_slots'])
    assert comparison['mesh_selection_differs_from_lod0'], 'No independent LOD geometry observed'
    comparisons.append(comparison)
    rows.append(dict(id=request['id'],prefab_path=request['prefab_path'],requested_lod=request['requested_lod'],native_receipt=case['receipt'],native_receipt_sha256=sha(case['receipt']),images=native['images'],lod_selection=comparison))
assert len(rows)==6 and len(controls)==3
for p in [Path(__file__),guardpath,CAP/'PREPARED.v1.json',CAP/'RUN-RESULT.v1.json']:pins[str(p)]=sha(p)
for p,h in pins.items():assert sha(p)==h,p
O.mkdir();sheets=[]
for group,start in enumerate(range(0,len(rows),3)):
    selected=rows[start:start+3];sheet=Image.new('RGB',(1024,540*len(selected)),(35,35,35));draw=ImageDraw.Draw(sheet)
    for i,row in enumerate(selected):
        draw.text((8,540*i+6),Path(row['prefab_path']).stem+' LOD'+str(row['requested_lod']),fill='white')
        for im in row['images']:
            with Image.open(im['path']) as png:sheet.paste(png.convert('RGB'),(512*im['view'],540*i+28))
    p=O/f'contact-sheet-{group}.png';sheet.save(p);pins[str(p)]=sha(p);sheets.append(str(p))
result=dict(status='six_lod_variants_reopened_visual_review_pending',cards_ready_for_review=6,cards_refused=0,rows=rows,refusals=[],contact_sheets=sheets,lod_comparisons=comparisons,input_pins=pins,private_import_migration_receipt=str(guardpath),agent_review=False,human_review=False,original_version_pipeline_equivalence=False,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat())
(O/'CANDIDATES.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps(dict(cards=6,views=12,lod_comparisons=comparisons)))
