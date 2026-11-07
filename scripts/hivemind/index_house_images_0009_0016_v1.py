"""Record sixteen inspected source views and index only their eight existing cards."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import subprocess
import sys

S = Path(__file__).parent
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'house-source-thumbnail-batch-0009-0016-review-v1'


def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()


candidate = json.loads((ROOT / 'CANDIDATES.v1.json').read_text(encoding='utf-8'))
assert candidate['status'] == 'sixteen_pngs_decoded_agent_visual_review_pending'
pins = dict(candidate['input_pins'])
for sheet in candidate['contact_sheets']:
    assert sha(sheet['path']) == sheet['sha256']
    pins[sheet['path']] = sheet['sha256']
for p, h in pins.items():
    assert sha(p) == h, p
observations = [
    'Two complete, uncropped views of a low green bush with yellow flowering branches. Leaf/flower silhouette is visible against the background.',
    'Two complete views of the foliage variant, with green leaves and yellow flowering branches. Variant identity is retained; wind behaviour is not qualified by these stills.',
    'Two complete views of a smaller flowering green bush, with upright yellow flowering branches and visible open silhouette.',
    'Two complete views of a low broad green bush with upright leafy shoots; leaf silhouette is visible without pink error material or blank output.',
    'Two complete views of a taller rounded green bush, with individual leafy shoots visible around its outline.',
    'Two complete views of the corresponding tall foliage variant. Leaves and outline are visible; source variant remains a separate card.',
    'Two complete views of a small open green bush with distinct upright shoots and visible leaves.',
    'Two complete views of the low-wind variant, with green leafy shoots. Appearance is readable; animation and wind amplitude remain unverified.',
]
assert len(candidate['rows']) == len(observations) == 8
for row, observed in zip(candidate['rows'], observations):
    row.update(observed=observed, agent_review=True, human_review=False)
pins[str(Path(__file__))] = sha(__file__)
review = dict(status='eight_house_source_card_thumbnail_pairs_reopened_and_agent_reviewed',
              finished_utc=datetime.now(timezone.utc).isoformat(), sub_batch='L08.IMAGE.HOUSE.03',
              cards_verified=8, images_verified=16, cards_refused=0, rows=candidate['rows'], input_pins=pins,
              source_inputs_unchanged=True, presentation_only=True, original_version_parity=False,
              agent_review=True, human_review=False, images_installed_in_vault=False, forge_editor_qualified=False, full_import=False)
review_path = ROOT / 'RESULT.v1.json'
assert not review_path.exists()
review_path.write_text(json.dumps(review, indent=2), encoding='utf-8')
helper = (S / 'index_house_thumbnail_batch_v1.py').read_text(encoding='utf-8')
helper = helper.replace('house-source-thumbnail-batch-0001-0008-index-v1', 'house-source-thumbnail-batch-0009-0016-index-v1')
helper = helper.replace('house-source-thumbnail-batch-0001-0008-review-v2', 'house-source-thumbnail-batch-0009-0016-review-v1')
helper = helper.replace('house-source-thumbnail-batch-0001-0008-review-v3', 'house-source-thumbnail-batch-0009-0016-review-v1')
replacements = {
    "review['cards_verified']==5": "review['cards_verified']==8", "review['images_verified']==10": "review['images_verified']==16",
    "review['cards_refused']==3": "review['cards_refused']==0", 'len(ids)==5': 'len(ids)==8',
    'updated=5': 'updated=8', 'five_existing_source_card_thumbnails': 'eight_existing_source_card_thumbnails',
    'five_house_source_cards_transactionally_indexed_three_refusals_retained': 'eight_house_source_cards_transactionally_indexed',
    'cards_updated=5': 'cards_updated=8', 'images_installed=10': 'images_installed=16', 'cards_refused=3': 'cards_refused=0',
    'missing_primary_images=942': "missing_primary_images=sum(not a.get('preview') for a in final['catalog']['assets'])",
}
for old, new in replacements.items():
    assert old in helper, old
    helper = helper.replace(old, new)
target = S / 'apply_house_images_0009_0016_v1.py'
assert not target.exists()
target.write_text(helper, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
