"""Seal the agent review of 12 actual source LOD variant views."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');O=W/'pirate-lod-combined-review-v357'
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
p=W/'pirate-lod-thumbnail-review-v356/CANDIDATES.v1.json';r=json.loads(p.read_text('utf-8-sig'))
assert not O.exists() and len(r['rows'])==6 and not r['refusals']
assert sum(len(a['images']) for a in r['rows'])==12
assert all(a['lod_selection']['mesh_selection_differs_from_lod0'] for a in r['rows'])
for f,h in r['input_pins'].items():assert sha(Path(f))==h,f
r['input_pins'][str(p)]=sha(p);r['input_pins'][str(Path(__file__))]=sha(Path(__file__))
r.update(status='six_actual_lod_variants_agent_reviewed',cards_verified=6,images_verified=12,agent_review=True,human_review=False,conversion_qualified=False,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat(),visual_observations='All 12 variant views inspected. Barrel tabletop retains bottles and candles. Hanging wheel chandelier retains ropes and candles. Pier stair retains steps, rails and supports. Complete framing and textures; no pink shader failure, uniform image or cut-off observed. LOD1 and LOD2 source mesh identities separately verified. No flame, animation, automatic LOD, product renderer or runtime qualification.')
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(r,indent=2),'utf-8')
print('Six actual LOD variants and twelve images reviewed')
