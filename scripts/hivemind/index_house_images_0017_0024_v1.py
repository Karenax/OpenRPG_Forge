"""Index the eight newly inspected source cards, preserving their variant identities."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import subprocess
import sys

S = Path(__file__).parent
ROOT = Path('F:/OpenRPG_ImportWork/house-source-thumbnail-batch-0017-0024-review-v1')


def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()


candidate = json.loads((ROOT / 'CANDIDATES.v1.json').read_text(encoding='utf-8'))
pins = dict(candidate['input_pins'])
for sheet in candidate['contact_sheets']:
    assert sha(sheet['path']) == sheet['sha256']
    pins[sheet['path']] = sheet['sha256']
for p, h in pins.items():
    assert sha(p) == h, p
observations = [
    'Two complete views of the small low-wind foliage variant, with visible green leaves and upright shoots. Wind behaviour remains unverified.',
    'Two complete views of a grey rounded gravestone; fissures and the engraved face are visible, with no clipping.',
    'Two complete views of four grey square paving plates; their edges and surface marks are visible.',
    'Two complete views of green stems with purple flowers; foliage cutouts and the full silhouette remain visible.',
    'Two complete views of the corresponding purple-flowered foliage variant; its separate source identity is retained.',
    'Two complete views of a stone house, with tiled roof, chimneys, dormers and purple shutters visible. This is a source thumbnail, not a runtime building qualification.',
    'Two complete views of a green leafy bush with multiple blue flower heads, retaining the full silhouette.',
    'Two complete views of the pink/purple flower-head variant, with green leaves and complete silhouette. No variant identities are merged.',
]
assert len(candidate['rows']) == len(observations) == 8
for row, observed in zip(candidate['rows'], observations):
    row.update(observed=observed, agent_review=True, human_review=False)
pins[str(Path(__file__))] = sha(__file__)
review = dict(status='eight_house_source_card_thumbnail_pairs_reopened_and_agent_reviewed',
              finished_utc=datetime.now(timezone.utc).isoformat(), sub_batch='L08.IMAGE.HOUSE.04',
              cards_verified=8, images_verified=16, cards_refused=0, rows=candidate['rows'], input_pins=pins,
              source_inputs_unchanged=True, presentation_only=True, original_version_parity=False,
              agent_review=True, human_review=False, images_installed_in_vault=False, forge_editor_qualified=False, full_import=False)
path = ROOT / 'RESULT.v1.json'
assert not path.exists()
path.write_text(json.dumps(review, indent=2), encoding='utf-8')
helper = (S / 'apply_house_images_0009_0016_v1.py').read_text(encoding='utf-8')
helper = helper.replace('house-source-thumbnail-batch-0009-0016', 'house-source-thumbnail-batch-0017-0024')
target = S / 'apply_house_images_0017_0024_v1.py'
assert not target.exists()
target.write_text(helper, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
