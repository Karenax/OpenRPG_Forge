"""Seal actual inspection of admitted batch06 views; keep palm refusals intact."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json
W=Path('F:/OpenRPG_ImportWork')
O=W/'pirate-lod-batch06-admitted-review-v519'
p=W/'pirate-lod-thumbnail-batch06-admitted-review-v517/CANDIDATES.v1.json'
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
r=json.loads(p.read_text('utf-8-sig'))
assert not O.exists() and len(r['rows'])==r['cards_ready_for_review']==10
assert r['cards_refused']==len(r['refusals'])==6
assert {a['id'] for a in r['rows']}.isdisjoint(a['id'] for a in r['refusals'])
assert sum(len(a['images']) for a in r['rows'])==20
assert all(a['lod_selection']['mesh_selection_differs_from_lod0'] for a in r['rows'])
for f,h in r['input_pins'].items():assert sha(f)==h,f
r['input_pins'][str(p)]=sha(p)
r['input_pins'][str(Path(__file__))]=sha(Path(__file__))
r.update(status='ten_admitted_lod_variants_agent_reviewed_six_palm_refusals_retained',cards_verified=10,images_verified=20,agent_review=True,human_review=False,conversion_qualified=False,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat(),visual_observations='All 20 admitted views inspected across four original-size sheets. The lookout tower retains roof, platform, railings, braces and rope ties. The cage support retains its beam, brace, ties and hanging chain. Three cliff variants retain distinct clustered or vertical rock silhouettes and textured surfaces. Both views are fully framed and recognizable; no pink shader failure, uniform image or cut-off observed. LOD1/2 mesh identities differ from LOD0 controls. Six palm variants remain refused for dedicated source material migration qualification. Unity presentation captures do not qualify Forge rendering, runtime placement or Studio GUI.')
O.mkdir()
(O/'RESULT.v1.json').write_text(json.dumps(r,indent=2),'utf-8')
print('Ten admitted variants and twenty views reviewed; six palm refusals retained')
