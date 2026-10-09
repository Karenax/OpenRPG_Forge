"""Verify only new durable library pages; retain the previous evidence scope."""
from pathlib import Path
import hashlib
import json
import datetime

WORK = Path('F:/OpenRPG_ImportWork')
ROOT = WORK / 'library-checkpointed-native-audit-v2'
PRIOR = WORK / 'library-available-images-independent-reopen-v2/RESULT.v1.json'
OUT = WORK / 'library-incremental-pages-reopen-v3'

def sha(path):
    h = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()

def main():
    assert not OUT.exists(), 'Preserve existing receipts'
    OUT.mkdir()
    result = dict(status='checking_new_durable_pages', full_import=False,
                  gpu_qualified=False, all_images_available=False)
    try:
        prior_raw = PRIOR.read_bytes()
        prior = json.loads(prior_raw)
        assert prior['status'] == 'available_thumbnail_bytes_reopened_missing_images_retained'
        assert prior['cards_checked'] == 24576 and prior['expected_cards'] == 26735
        ids = set()
        for batch in prior['batches']:
            assert sha(batch['path']) == batch['sha256'], batch['path']
            for row in json.loads(Path(batch['path']).read_bytes())['rows']:
                assert row['id'] not in ids
                ids.add(row['id'])
        assert len(ids) == prior['cards_checked']
        pages = sorted((ROOT / 'batches').glob('*.json'))
        offset = prior['cards_checked']
        batches, missing, images = [], [], {}
        references = 0
        for page in pages:
            raw = page.read_bytes()
            record = json.loads(raw)
            if record['offset'] < prior['cards_checked']:
                continue
            rows = record['rows']
            assert record['offset'] == offset and record['expected_total'] == 26735
            assert record['revision'] == prior['revision']
            assert record['details_checked'] == len(rows) and 0 < len(rows) <= 48
            assert record['only_library_records_and_image_bytes']
            assert not record['full_import'] and not record['gpu_qualified']
            page_images = 0
            for row in rows:
                assert row['id'] not in ids
                ids.add(row['id'])
                path, digest = row['preview_path'], row['thumbnail_sha256']
                if path is None or digest is None:
                    assert path is None and digest is None
                    missing.append(dict(id=row['id'], pack=row['pack'], availability=row['availability'],
                                        reason='native_card_has_no_thumbnail'))
                else:
                    if path not in images:
                        images[path] = sha(path)
                    assert images[path] == digest, row['id']
                    page_images += 1
            assert page_images == record['images_read']
            assert sha(page) == hashlib.sha256(raw).hexdigest()
            batches.append(dict(path=str(page), sha256=hashlib.sha256(raw).hexdigest(),
                                cards=len(rows), images=page_images))
            references += page_images
            offset += len(rows)
        assert offset == len(ids) <= 26735
        assert offset - prior['cards_checked'] == references + len(missing)
        result.update(status='new_library_pages_and_thumbnail_bytes_verified',
                      prior_receipt=str(PRIOR), prior_receipt_sha256=hashlib.sha256(prior_raw).hexdigest(),
                      inherited_cards=prior['cards_checked'], new_cards=offset-prior['cards_checked'],
                      new_image_references=references, new_distinct_images_hashed=len(images),
                      new_missing_images=len(missing), cumulative_cards=offset,
                      cumulative_image_references=prior['images_checked']+references,
                      cumulative_missing_images=prior['missing_images']+len(missing),
                      expected_cards=26735, all_cards_observed=offset==26735,
                      prior_image_bytes_rehashed=False, prior_pages_rehashed=True,
                      revision=prior['revision'], batches=batches, new_image_sha256=images,
                      new_missing_image_cards=missing)
    except Exception as error:
        result.update(status='refused_or_unverified', error=repr(error))
    result.update(finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  script_sha256=sha(Path(__file__).resolve()))
    (OUT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in {
        'batches','new_image_sha256','new_missing_image_cards'}}, ensure_ascii=False))

if __name__ == '__main__':
    main()
