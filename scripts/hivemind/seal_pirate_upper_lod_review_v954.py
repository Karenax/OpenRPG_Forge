"""Record actual inspection of eight contact sheets and 44 source views."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json

W = Path('F:/OpenRPG_ImportWork')
O = W/'pirate-upper-lod-images-reviewed-v954'
p = W/'pirate-upper-lod-images-review-v953/CANDIDATES.v1.json'

def sha(path):
    with Path(path).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()

r = json.loads(p.read_text('utf-8'))
assert not O.exists() and len(r['rows']) == 22 and len(r['refusals']) == 2
assert len(r['contact_sheets']) == 8 and sum(len(row['images']) for row in r['rows']) == 44
assert all(row['lod_selection']['mesh_selection_differs_from_lod0'] for row in r['rows'])
for path, digest in r['input_pins'].items():
    assert sha(path) == digest, path
r['input_pins'][str(p)] = sha(p)
r['input_pins'][str(Path(__file__).resolve())] = sha(Path(__file__))
r.update(status='twenty_two_upper_lod_source_cards_agent_reviewed', cards_verified=22, images_verified=44,
         agent_review=True, human_review=False, conversion_qualified=False, full_import=False,
         finished_utc=datetime.now(timezone.utc).isoformat(),
         visual_observations='All 44 views inspected on eight sheets. Pier stairs retain steps, legs and diagonal supports; wooden and fabric awnings retain their posts and roofs. Four bottles retain distinct colors, corks and wrapping with visibly simplified source geometry. Two stone buildings retain walls, roofs, openings and the annex. Doors retain distinct front and rear surfaces. Fence post, rope fence and wicker fence retain their source shapes with coarse upper-LOD simplification. All objects are fully framed; no uniform image, pink shader failure or clipped silhouette observed. Exact selected LOD3/4/5 mesh identities differ from LOD0. BarrelTable and Chandelier LOD3 refusals remain excluded. This qualifies source thumbnails only, not conversion fidelity, OpenRPG rendering, placement, runtime or actual Studio GUI.')
O.mkdir()
(O/'RESULT.v1.json').write_text(json.dumps(r, indent=2), 'utf-8')
print('22 source cards / 44 views reviewed; 2 native refusals retained')
