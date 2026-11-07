"""Record the visual inspection of 14 source views, without conversion claims."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');O=W/'pirate-p15-combined-review-v337'
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
p=W/'pirate-direct-thumbnail-review-v336/CANDIDATES.v1.json'
r=json.loads(p.read_text('utf-8-sig'))
assert not O.exists() and len(r['rows'])==7 and not r['refusals']
assert len({a['id'] for a in r['rows']})==7 and sum(len(a['images']) for a in r['rows'])==14
for f,h in r['input_pins'].items():assert sha(Path(f))==h,f
r['input_pins'][str(p)]=sha(p);r['input_pins'][str(Path(__file__))]=sha(Path(__file__))
r.update(status='seven_p15_source_thumbnails_agent_reviewed',cards_verified=7,images_verified=14,cards_refused=0,agent_review=True,human_review=False,conversion_qualified=False,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat(),visual_observations='All 14 views inspected. Five distinct posts or beams retain rope and metal variants. One high square stool and one low round stool are distinct. Complete framing, visible textures; no pink shader failure, uniform image or cut-off observed. Static source thumbnail review only.')
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(r,indent=2),'utf-8')
print('Seven cards and fourteen views reviewed')
