"""Reopen the exact EV8 comparison, refusing unstable views or image defects."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
from PIL import Image,ImageDraw
W=Path('F:/OpenRPG_ImportWork');CAP=W/'pirate-shipwreck-migration-render-v298';O=W/'pirate-shipwreck-migration-review-v300'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
run=read(CAP/'RUN-RESULT.v1.json');prep=read(CAP/'PREPARED.v1.json')
assert run['status']=='pirate_migration_native_comparison_completed' and run['unity_exit_code']==0 and not run['input_changes']
pins=prep['input_pins']|run['output_pins']
for p,h in pins.items():assert sha(p)==h,p
rows=[];refusals=[];contact=Image.new('RGB',(1024,540),(35,35,35));draw=ImageDraw.Draw(contact)
for i in range(1):
    cases=prep['cases'][i*4+1:(i+1)*4];receipts=[read(c['output']) for c in cases]
    asset_id=cases[0]['id'].rsplit(':',1)[0];errors=[];pairs=[]
    assert all(r['presentation_fixed_ev']==8 and r['presentation_tonemapping']=='Neutral' for r in receipts)
    assert all(r['source_object_unchanged'] and r['preview_scene_isolated'] for r in receipts)
    assert all(r['compiled_passes'] and all(p['compiled'] for p in r['compiled_passes']) for r in receipts)
    for view in range(2):
        images=[r['images'][view] for r in receipts];raw=[Path(a['raw_path']).read_bytes() for a in images]
        assert all(len(b)==512*512*8 for b in raw)
        pair=dict(view=view,migration_exact=raw[0]==raw[1],repeat_exact=raw[0]==raw[2])
        pairs.append(pair)
        if not pair['migration_exact'] or not pair['repeat_exact']:errors.append('Raw comparison not exact or baseline unstable in view '+str(view))
        image=images[0]
        with Image.open(image['path']) as png:
            png.load();assert png.format=='PNG' and png.size==(512,512)
            if png.convert('RGB').getcolors(1) is not None:errors.append('Uniform PNG')
            contact.paste(png.convert('RGB'),(512*view,540*i+28))
        if image['clipped_rgb_pixels']!=0 or image['linear_max_channel']>1:errors.append('Clipped image/highlights')
    draw.text((8,540*i+6),Path(cases[0]['prefab_path']).stem,fill=(255,255,255))
    if errors:refusals.append(dict(id=asset_id,stage='migration_render_and_png_reopen',reason=errors,comparisons=pairs))
    else:rows.append(dict(id=asset_id,prefab_path=cases[0]['prefab_path'],native_receipt=cases[0]['output'],
        native_receipt_sha256=sha(cases[0]['output']),images=receipts[0]['images'],comparisons=pairs))
O.mkdir();contact.save(O/'contact-sheet.png')
for p in [Path(__file__),CAP/'RUN-RESULT.v1.json',CAP/'PREPARED.v1.json',O/'contact-sheet.png']:
    pins[str(p)]=sha(p)
result=dict(status='migration_comparison_and_images_reopened_visual_review_pending',rows=rows,refusals=refusals,
    cards_ready_for_review=len(rows),cards_refused=len(refusals),private_import_migration_receipt=str(CAP/'RUN-RESULT.v1.json'),
    input_pins=pins,agent_review=False,human_review=False,full_import=False,
    original_version_pipeline_equivalence=False,finished_utc=datetime.now(timezone.utc).isoformat())
(O/'CANDIDATES.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps(dict(ready=len(rows),refused=refusals,contact_sheet=str(O/'contact-sheet.png'))))
