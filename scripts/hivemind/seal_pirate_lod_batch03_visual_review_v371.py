"""Seal the agent review of 12 actual source LOD variant views."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');O=W/'pirate-lod-batch03-combined-review-v371'
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
p=W/'pirate-lod-thumbnail-batch03-review-v370/CANDIDATES.v1.json';r=json.loads(p.read_text('utf-8-sig'))
assert not O.exists() and len(r['rows'])==16 and not r['refusals']
assert sum(len(a['images']) for a in r['rows'])==32
assert all(a['lod_selection']['mesh_selection_differs_from_lod0'] for a in r['rows'])
for f,h in r['input_pins'].items():assert sha(Path(f))==h,f
r['input_pins'][str(p)]=sha(p);r['input_pins'][str(Path(__file__))]=sha(Path(__file__))
r.update(status='sixteen_actual_lod_variants_agent_reviewed',cards_verified=16,images_verified=32,agent_review=True,human_review=False,conversion_qualified=False,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat(),visual_observations='All 32 variant views inspected. Stone building retains roofs, arch and annex. Closed crate retains plank sides and diagonal braces. Shack door has distinct front/back planks and braces. Fence post variants retain rope bands; rope fence retains three posts and spanning ropes. Two wicker fence lengths retain sparse woven rails. LOD2 shows source simplification of ropes and rails. Complete framing and textures; no pink shader failure, uniform image or cut-off. Each LOD1/2 mesh identity verified against its LOD0 control. No automatic LOD, product renderer or runtime qualification.')
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(r,indent=2),'utf-8')
print('Sixteen actual LOD variants and thirty-two images reviewed')
