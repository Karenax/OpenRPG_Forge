"""Reopen the existing current captures against exact scoped material evidence."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib
from PIL import Image,ImageDraw
W=Path('F:/OpenRPG_ImportWork');CAP=W/'pirate-wood-lod-migration-v591';O=W/'pirate-wood-lod-images-review-v594'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
proofpath=W/'pirate-wood-lod-migration-review-v592/RESULT.v1.json';proof=read(proofpath)
assert proof['source_image_migration_scope_verified'] and proof['groups_verified']==12 and proof['lod_cases_verified']==36
prep=read(CAP/'PREPARED.v1.json');pins=dict(proof['input_pins']);rows=[];controls={};comparisons=[]
for i,verified in enumerate(proof['rows']):
    request=prep['cases'][4*i+1];p=Path(request['output']);native=read(p)
    assert native['id']==request['id']==verified['source_card_id']+':current'
    assert native['prefab_guid']==request['prefab_guid'] and native['requested_lod']==verified['requested_lod']
    assert native['requested_lod_selection_verified'] and native['selected_mesh_identities']==verified['selected_mesh_identities']
    assert native['selected_mesh_vertex_counts']==verified['selected_mesh_vertex_counts']
    assert native['source_object_unchanged'] and native['preview_scene_isolated'] and native['presentation_only']
    assert native['presentation_fixed_ev']==8 and native['gpu']=='Direct3D11'
    source_meshes={r['mesh_guid']+':'+r['mesh_file_id'] for r in native['renderers']}
    assert set(native['selected_mesh_identities'])<=source_meshes
    assert all(p['compiled'] for p in native['compiled_passes'])
    for image in native['images']:
        path=Path(image['path']);assert sha(path)==image['sha256'];pins[str(path)]=image['sha256']
        assert image['clipped_rgb_pixels']==0 and image['linear_max_channel']<=1
        with Image.open(path) as png:
            png.load();assert png.format=='PNG' and png.size==(512,512) and png.convert('RGB').getcolors(1) is None
    pins[str(p)]=sha(p)
    if native['requested_lod']==0:controls[request['prefab_guid']]=native;continue
    control=controls[request['prefab_guid']]
    comparison=dict(id=verified['source_card_id'],requested_lod=native['requested_lod'],selected_mesh_identities=native['selected_mesh_identities'],lod0_mesh_identities=control['selected_mesh_identities'],requested_vertices=sum(native['selected_mesh_vertex_counts']),lod0_vertices=sum(control['selected_mesh_vertex_counts']),mesh_selection_differs_from_lod0=native['selected_mesh_identities']!=control['selected_mesh_identities'],source_empty_lod_slots=native['source_empty_lod_slots'])
    assert comparison['mesh_selection_differs_from_lod0']
    comparisons.append(comparison)
    rows.append(dict(id=verified['source_card_id'],prefab_path=request['prefab_path'],requested_lod=native['requested_lod'],native_receipt=str(p),native_receipt_sha256=sha(p),images=native['images'],lod_selection=comparison))
assert len(rows)==24 and len(controls)==12
for p in [Path(__file__),proofpath,CAP/'PREPARED.v1.json']:pins[str(p)]=sha(p)
for p,h in pins.items():assert sha(p)==h,p
O.mkdir();sheets=[]
for group,start in enumerate(range(0,len(rows),3)):
    selected=rows[start:start+3];sheet=Image.new('RGB',(1024,540*len(selected)),(35,35,35));draw=ImageDraw.Draw(sheet)
    for i,row in enumerate(selected):
        draw.text((8,540*i+6),Path(row['prefab_path']).stem+' LOD'+str(row['requested_lod']),fill='white')
        for im in row['images']:
            with Image.open(im['path']) as png:sheet.paste(png.convert('RGB'),(512*im['view'],540*i+28))
    p=O/f'contact-sheet-{group}.png';sheet.save(p);pins[str(p)]=sha(p);sheets.append(str(p))
result=dict(status='twenty_four_wood_lod_variants_reopened_visual_review_pending',cards_ready_for_review=24,cards_refused=0,rows=rows,refusals=[],contact_sheets=sheets,lod_comparisons=comparisons,input_pins=pins,private_import_migration_receipt=str(proofpath),agent_review=False,human_review=False,original_version_pipeline_equivalence=False,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat())
(O/'CANDIDATES.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps(dict(cards=24,views=48,sheets=sheets)))
