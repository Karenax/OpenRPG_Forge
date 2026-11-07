"""Merge accepted EV8 objects and EV4 rocks, keeping all refused captures."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
from PIL import Image, ImageDraw

W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'house-source-thumbnail-batch-0033-0040-final-review-v1'
assert not ROOT.exists()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
original=W/'house-source-thumbnail-batch-0033-0040-review-v1/CANDIDATES.v1.json'
retry=W/'house-source-thumbnail-batch-0033-0040-rocks-ev4-review-v1/CANDIDATES.v1.json'
a,b=read(original),read(retry)
assert len(a['rows'])==8 and len(b['rows'])==4
pins={**a['input_pins'],**b['input_pins']}
for p in [Path(__file__),original,retry]:pins[str(p)]=sha(p)
for i in range(4):
    assert a['rows'][i+3]['id']==b['rows'][i]['id']
    a['rows'][i+3]=b['rows'][i]
rows=a['rows']
assert len({row['id'] for row in rows})==8
for p,h in pins.items():assert sha(Path(p))==h,p
ROOT.mkdir()
sheets=[]
for group in range(2):
    sheet=Image.new('RGB',(1024,2160),'#232323');draw=ImageDraw.Draw(sheet)
    for index in range(group*4,group*4+4):
        row=rows[index];native=read(Path(row['native_receipt']))
        expected_ev=4 if 3<=index<=6 else 8
        assert native['presentation_fixed_ev']==expected_ev and native['source_object_unchanged']
        draw.text((8,(index%4)*540+6),row['id']+f' / EV{expected_ev}',fill='white')
        for im in row['images']:
            assert im['clipped_rgb_pixels']==0 and im['linear_max_channel']<=1
            p=Path(im['path']);assert sha(p)==im['sha256']
            with Image.open(p) as image:
                image.load();assert image.size==(512,512)
                sheet.paste(image.convert('RGB'),(512*im['view'],(index%4)*540+28))
    p=ROOT/f'contact-sheet-{group}.png';sheet.save(p)
    sheets.append(dict(path=str(p),sha256=sha(p)))
    pins[str(p)]=sha(p)
(ROOT/'CANDIDATES.v1.json').write_text(json.dumps(dict(status='eight_mixed_exposure_source_card_pairs_visual_review_pending',finished_utc=datetime.now(timezone.utc).isoformat(),rows=rows,contact_sheets=sheets,input_pins=pins,original_dark_and_clipped_captures_retained=True,agent_review=False,human_review=False,full_import=False),indent=2),encoding='utf-8')
print(json.dumps(dict(cards=8,images=16,sheets=sheets)))
