"""Record the visual inspection of 14 source views, without conversion claims."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');O=W/'pirate-glass-sdk-combined-review-v345'
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
p=W/'pirate-glass-sdk-review-v344/CANDIDATES.v1.json'
r=json.loads(p.read_text('utf-8-sig'))
assert not O.exists() and len(r['rows'])==1 and not r['refusals']
assert len({a['id'] for a in r['rows']})==1 and sum(len(a['images']) for a in r['rows'])==2
for f,h in r['input_pins'].items():assert sha(Path(f))==h,f
r['input_pins'][str(p)]=sha(p);r['input_pins'][str(Path(__file__))]=sha(Path(__file__))
r.update(status='glass_sdk_source_thumbnail_agent_reviewed',cards_verified=1,images_verified=2,cards_refused=0,agent_review=True,human_review=False,conversion_qualified=False,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat(),visual_observations='Both views inspected: complete textured wooden window frame, mullions and panes, no pink shader failure, uniform image or cut-off. Glass optical fidelity and product rendering are unqualified. SDK-derived transparent blend factor and opaque negative control passed; native raw comparisons and repeats exact.')
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(r,indent=2),'utf-8')
print('One window card and two views reviewed')
