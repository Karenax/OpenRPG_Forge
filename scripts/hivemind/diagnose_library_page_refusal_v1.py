"""Preserve and identify a durable page refusal without changing the Vault."""
from pathlib import Path
import datetime
import hashlib
import json

ROOT = Path('F:/OpenRPG_ImportWork/library-checkpointed-native-audit-v2')
OUT = ROOT.parent/'library-page-count-refusal-v1'

if __name__ == '__main__':
    assert not OUT.exists()
    pages = sorted((ROOT/'batches').glob('*.json'))
    failures = []
    for page in pages:
        raw = page.read_bytes()
        record = json.loads(raw)
        if not record['details_checked'] == record['images_read'] == len(record['rows']):
            failures.append(dict(page=str(page), sha256=hashlib.sha256(raw).hexdigest(), record=record))
    assert failures, 'No mismatch in current immutable pages'
    OUT.mkdir()
    result = dict(status='durable_library_page_count_mismatch_identified', pages_observed=len(pages),
        failed_pages=failures, previously_verified_checkpoint_preserved=True, full_import=False,
        finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (OUT/'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    for row in failures:
        r = row['record']
        print(json.dumps(dict(page=row['page'], offset=r['offset'], details_checked=r['details_checked'],
            images_read=r['images_read'], rows=len(r['rows']), other_fields={k:v for k,v in r.items() if k not in {'rows','offset','details_checked','images_read'}})))
