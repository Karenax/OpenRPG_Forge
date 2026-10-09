"""Reopen durable native audit pages and actual thumbnail bytes; no Vault writes."""
import datetime
import hashlib
import json
from pathlib import Path

ROOT = Path('F:/OpenRPG_ImportWork/library-checkpointed-native-audit-v2')


def sha(path):
    digest = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


def main():
    pages = sorted((ROOT / 'batches').glob('*.json'))
    assert pages, 'No durable native audit pages'
    batches, identities, revision, offset = [], set(), None, 0
    for page in pages:
        raw = page.read_bytes()
        record = json.loads(raw)
        rows = record['rows']
        assert record['offset'] == offset
        assert record['expected_total'] == 26735
        assert record['only_library_records_and_image_bytes']
        assert not record['full_import'] and not record['gpu_qualified']
        assert record['details_checked'] == record['images_read'] == len(rows)
        assert 0 < len(rows) <= 48
        if revision is None:
            revision = record['revision']
        assert record['revision'] == revision
        for row in rows:
            assert row['id'] not in identities
            identities.add(row['id'])
            assert sha(row['preview_path']) == row['thumbnail_sha256']
        page_sha = hashlib.sha256(raw).hexdigest()
        assert sha(page) == page_sha, 'Durable page changed during reopening'
        batches.append(dict(file=str(page), sha256=page_sha, offset=offset,
                            details_checked=len(rows), images_read=len(rows), revision=revision))
        offset += len(rows)
    assert offset == len(identities) <= 26735
    receipts = sorted(ROOT.glob('INDEPENDENT-CHECKPOINT-REOPEN.v*.json'),
                      key=lambda p: int(p.name.split('.v')[1].split('.')[0]))
    if receipts:
        previous = json.loads(receipts[-1].read_bytes())
        assert offset >= previous['details_checked']
        if offset == previous['details_checked']:
            print(json.dumps(dict(status='existing_scope_rechecked', details_checked=offset)))
            return
    version = 1 + (int(receipts[-1].name.split('.v')[1].split('.')[0]) if receipts else 0)
    result = dict(schema='openrpg.library-checkpoint-independent-reopen.v1',
                  observed_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  status='durable_checkpoints_and_thumbnail_bytes_reopened',
                  details_checked=offset, images_read=offset, expected_total=26735,
                  batches=batches, unique_ids=len(identities),
                  only_library_records_and_image_bytes=True,
                  payload_archive_integrity_qualified=False, gpu_qualified=False,
                  full_import=False, reopening_script=str(Path(__file__).resolve()),
                  reopening_script_sha256=sha(__file__))
    target = ROOT / f'INDEPENDENT-CHECKPOINT-REOPEN.v{version}.json'
    with target.open('x', encoding='utf-8') as stream:
        json.dump(result, stream, ensure_ascii=False, indent=2)
    print(json.dumps(dict(receipt=str(target), details_checked=offset, full_import=False)))


if __name__ == '__main__':
    main()
