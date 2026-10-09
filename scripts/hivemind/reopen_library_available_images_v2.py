"""Separate checked cards, real thumbnail bytes, and explicitly missing images.

An absent thumbnail is never counted as an image or a completed library item.
Hash each shared image once in this run while retaining every card identity.
"""
from pathlib import Path
import datetime
import hashlib
import json
import subprocess
import sys

ROOT = Path('F:/OpenRPG_ImportWork/library-checkpointed-native-audit-v2')
OUT = ROOT.parent/'library-available-images-independent-reopen-v2'

def sha(path):
    digest = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024*1024), b''):
            digest.update(block)
    return digest.hexdigest()

def main():
    assert not OUT.exists()
    OUT.mkdir()
    result = dict(status='checking_available_images_and_preserving_missing_ids', full_import=False,
        all_images_available=False, gpu_qualified=False, payload_archive_integrity_qualified=False)
    def save():
        temp = OUT/'RESULT.v1.json.tmp'
        temp.write_text(json.dumps(result, indent=2), 'utf-8')
        temp.replace(OUT/'RESULT.v1.json')
    save()
    try:
        pages = sorted((ROOT/'batches').glob('*.json'))
        ids = set()
        unique_images = {}
        batches = []
        missing = []
        offset = images = 0
        revision = None
        for page in pages:
            raw = page.read_bytes()
            record = json.loads(raw)
            rows = record['rows']
            assert record['offset'] == offset and record['expected_total'] == 26735
            assert record['details_checked'] == len(rows) and 0 < len(rows) <= 48
            assert record['only_library_records_and_image_bytes'] and not record['full_import'] and not record['gpu_qualified']
            if revision is None:
                revision = record['revision']
            assert record['revision'] == revision
            page_images = 0
            for row in rows:
                assert row['id'] not in ids
                ids.add(row['id'])
                path, digest = row['preview_path'], row['thumbnail_sha256']
                if path is None or digest is None:
                    assert path is None and digest is None
                    missing.append(dict(id=row['id'], pack=row['pack'], availability=row['availability'],
                        reason='native_card_has_no_thumbnail', page=str(page)))
                else:
                    if path not in unique_images:
                        unique_images[path] = sha(path)
                    assert unique_images[path] == digest, row['id']
                    page_images += 1
            assert page_images == record['images_read']
            assert sha(page) == hashlib.sha256(raw).hexdigest()
            batches.append(dict(path=str(page), sha256=hashlib.sha256(raw).hexdigest(),
                cards=len(rows), images=page_images))
            offset += len(rows)
            images += page_images
        assert offset == len(ids) == images + len(missing) and offset <= 26735
        result.update(status='available_thumbnail_bytes_reopened_missing_images_retained',
            cards_checked=offset, images_checked=images, distinct_images_checked=len(unique_images),
            missing_images=len(missing), missing_image_cards=missing, expected_cards=26735,
            all_cards_observed=offset==26735, all_images_available=offset==26735 and not missing,
            revision=revision, batches=batches, image_sha256=unique_images,
            reopening_script_sha256=sha(Path(__file__).resolve()))
    except Exception as error:
        result.update(status='refused_or_unverified', error=repr(error))
    result['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save()
    print(json.dumps({key:value for key,value in result.items() if key not in {'missing_image_cards','batches','image_sha256'}}))
    if result['status'] != 'refused_or_unverified':
        subprocess.run([sys.executable, '-B', str(Path(__file__).with_name('record_active_batch_focus.py'))], check=True)

if __name__ == '__main__':
    main()
