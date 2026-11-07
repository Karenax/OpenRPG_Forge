"""Resume after preparation-only string overlap; retain the reviewed receipt unchanged."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import subprocess
import sys

S = Path(__file__).parent
W = Path('F:/OpenRPG_ImportWork')
REVIEW = W / 'house-source-thumbnail-batch-0009-0016-review-v1/RESULT.v1.json'
review = json.loads(REVIEW.read_text(encoding='utf-8'))
assert review['cards_verified'] == 8 and review['images_verified'] == 16 and review['cards_refused'] == 0
for p, expected in review['input_pins'].items():
    with Path(p).open('rb') as f:
        assert hashlib.file_digest(f, 'sha256').hexdigest() == expected, p
refusal = REVIEW.parent / 'DERIVATION-REFUSAL.v1.json'
assert not refusal.exists()
refusal.write_text(json.dumps({'status': 'index_helper_derivation_refusal_retained_before_vault_mutation',
                   'finished_utc': datetime.now(timezone.utc).isoformat(), 'reason': 'Short updated=5 replacement overlapped cards_updated=5; no apply helper existed or ran.',
                   'vault_modified': False, 'review_retained_unchanged': True}, indent=2), encoding='utf-8')
helper = (S / 'index_house_thumbnail_batch_v1.py').read_text(encoding='utf-8')
replacements = {
    'house-source-thumbnail-batch-0001-0008-index-v1': 'house-source-thumbnail-batch-0009-0016-index-v1',
    'house-source-thumbnail-batch-0001-0008-review-v2': 'house-source-thumbnail-batch-0009-0016-review-v1',
    'house-source-thumbnail-batch-0001-0008-review-v3': 'house-source-thumbnail-batch-0009-0016-review-v1',
    "review['cards_verified']==5": "review['cards_verified']==8", "review['images_verified']==10": "review['images_verified']==16",
    "review['cards_refused']==3": "review['cards_refused']==0", 'len(ids)==5': 'len(ids)==8',
    'updated=5': 'updated=8', 'five_existing_source_card_thumbnails': 'eight_existing_source_card_thumbnails',
    'five_house_source_cards_transactionally_indexed_three_refusals_retained': 'eight_house_source_cards_transactionally_indexed',
    'cards_updated=5': 'cards_updated=8', 'images_installed=10': 'images_installed=16', 'cards_refused=3': 'cards_refused=0',
    'missing_primary_images=942': "missing_primary_images=sum(not a.get('preview') for a in final['catalog']['assets'])",
}
for old in sorted(replacements, key=len, reverse=True):
    assert old in helper, old
    helper = helper.replace(old, replacements[old])
target = S / 'apply_house_images_0009_0016_v1.py'
assert not target.exists()
target.write_text(helper, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
