"""Reopen PNGs only for source cards admitted by the independently rehashed guard."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
from PIL import Image,ImageDraw
W=Path('F:/OpenRPG_ImportWork');CAP=W/'pirate-source-thumbnail-batch-0121-0128-v201'
GUARD=W/'pirate-source-thumbnail-batch-0121-0128-guard-v202/RESULT.v1.json'
O=W/'pirate-source-thumbnail-batch-0121-0128-review-v203'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
guard=read(GUARD);assert guard['original_sources_unchanged']
pins=dict(guard['input_pins'])
for p,h in pins.items():assert sha(p)==h,p
batch=read(CAP/'NATIVE-RESULT.v1.json');prep=read(CAP/'PREPARED.v1.json');admitted=set(guard['admitted_ids'])
rows=[];refusals=list(guard['cases_refused_source_parity'])
for case,request in zip(batch['cases'],prep['cases']):
    assert case['id']==request['id']
    if case['id'] not in admitted:continue
    assert case['captured']
    native=read(case['receipt']);assert native['id']==case['id'] and native['prefab_guid']==request['prefab_guid']
    assert native['source_object_unchanged'] and native['preview_scene_isolated'] and native['gpu']=='Direct3D11'
    assert native['presentation_fixed_ev']==8 and native['presentation_tonemapping']=='Neutral'
    assert native['compiled_passes'] and all(p['compiled'] for p in native['compiled_passes'])
    assert len(native['images'])==2;errors=[]
    for image in native['images']:
        assert sha(image['path'])==image['sha256']
        with Image.open(image['path']) as png:
            png.load();assert png.format=='PNG' and png.size==(512,512)
            if png.convert('RGB').getcolors(1) is not None:errors.append('uniform image')
        if image['clipped_rgb_pixels']!=0 or image['linear_max_channel']>1:errors.append('clipped channel/highlights')
    if errors:refusals.append(dict(id=case['id'],stage='png_reopen',reason=errors))
    else:rows.append(dict(id=case['id'],prefab_path=request['prefab_path'],native_receipt=case['receipt'],native_receipt_sha256=sha(case['receipt']),images=native['images']))
assert rows
O.mkdir();sheet=Image.new('RGB',(1024,540*len(rows)),(35,35,35));draw=ImageDraw.Draw(sheet)
for i,row in enumerate(rows):
    draw.text((8,i*540+6),Path(row['prefab_path']).stem,fill=(255,255,255))
    for image in row['images']:
        with Image.open(image['path']) as png:sheet.paste(png.convert('RGB'),(image['view']*512,i*540+28))
sheet.save(O/'contact-sheet.png')
for p in [Path(__file__),GUARD,CAP/'PREPARED.v1.json',CAP/'RUN-RESULT.v1.json',CAP/'NATIVE-RESULT.v1.json',O/'contact-sheet.png']:pins[str(p)]=sha(p)
result=dict(status='independently_guarded_source_images_reopened_visual_review_pending',rows=rows,refusals=refusals,
    input_pins=pins,cards_ready_for_review=len(rows),cards_refused=len(refusals),private_import_migration_receipt=str(GUARD),
    agent_review=False,human_review=False,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat())
(O/'CANDIDATES.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps(dict(ready=len(rows),refused=len(refusals),contact_sheet=str(O/'contact-sheet.png'))))
