"""Reconcile completed byte audit with every previously verified library ID."""
from pathlib import Path
import collections
import datetime
import hashlib
import json

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'indexed-payload-integrity-independent-reopen-v3'


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def reconcile(rows, cards):
    seen = set()
    counts = collections.Counter()
    for row in rows:
        key = row['id']
        assert key in cards and key not in seen
        seen.add(key)
        card = cards[key]
        assert row['status'] == 'pass' and row['availability'] == card['availability']
        kind = row['availability']
        assert kind in ('converted_model', 'source_support')
        assert row['model_full_sha_checked'] is (kind == 'converted_model')
        assert row['source_and_meta_full_sha_checked'] is (kind == 'source_support')
        assert row['preview_png_sha_checked'] is bool(card.get('thumbnail_sha256'))
        assert card['license'] == 'LicenseRef-Unity-Asset-Store-EULA'
        counts[kind] += 1
        counts['preview_checked' if row['preview_png_sha_checked'] else 'preview_absent'] += 1
    assert seen == set(cards) and len(seen) == 26735
    return dict(counts)


def main():
    assert not ROOT.exists(), 'Immutable independent receipt already exists'
    audit_root = W / 'asset-library-indexed-payload-integrity-resume-v2'
    run = read(audit_root / 'RUN-RESULT.v1.json')
    payload_path = audit_root / 'PAYLOAD-RESULT.v1.json'
    assert run['actual_exit_code'] == 0 and run['indexed_payload_integrity_qualified']
    assert sha(payload_path) == run['payload_receipt_sha256']
    audit = read(payload_path)
    assert audit['indexed_count'] == audit['passed_count'] == 26735 and audit['refused_count'] == 0
    assert audit['unique_archive_payloads_hashed'] == run['archives_hashed'] == 38
    prior_path = W / 'library-all-records-independent-reconciliation-v4/RESULT.v1.json'
    prior = read(prior_path)
    assert prior['revision'] == audit['revision'] == run['registry_revision']
    cards, pins = {}, {}
    for page in prior['pages']:
        path = Path(page['path'])
        assert sha(path) == page['sha256']
        pins[str(path)] = page['sha256']
        data = read(path)
        assert data['revision'] == audit['revision']
        for card in data['rows']:
            assert card['id'] not in cards
            cards[card['id']] = card
    counts = reconcile(audit['rows'], cards)
    assert counts == dict(converted_model=14219, source_support=12516, preview_checked=25787, preview_absent=948)
    snapshot_path = W / 'native-model-transport-installed-v1/INSTALLED.v1.json'
    snapshots = read(snapshot_path)['preimages_and_finished_audit_inputs_preserved']
    old_pins = read(audit_root / 'PREPARED.v1.json')['input_pins']
    assert len(snapshots) == len(old_pins) == 11
    for saved in snapshots:
        assert saved['sha256'] == old_pins[saved['original']]
        assert sha(saved['snapshot']) == saved['sha256']
        pins[saved['snapshot']] = saved['sha256']
    # Registry bytes must still represent the audited snapshot. No Vault writes.
    for relative, field in [('metadata/asset-import-registry.v1.json', 'registry_sha256'),
                            ('metadata/asset-source-support-registry.v1.json', 'source_support_registry_sha256')]:
        path = Path('F:/OpenRPG_AssetVault') / relative
        assert sha(path) == audit[field]
        pins[str(path)] = audit[field]
    for path in [Path(__file__), payload_path, prior_path, snapshot_path, audit_root / 'RUN-RESULT.v1.json', audit_root / 'PREPARED.v1.json']:
        pins[str(path)] = sha(path)
    result = dict(status='all_indexed_payload_sha_receipts_reconciled_with_all_library_ids',
                  indexed_entries=26735, counts=counts, archives_payload_sha_verified=38,
                  refused_entries=0, registry_revision=audit['revision'],
                  finished_audit_source_inputs_preserved=11,
                  image_absences_explicitly_retained=948, no_vault_writes=True,
                  scope=audit['scope'], whole_current_vault_qualified=False,
                  raw_fbx_dependency_closures_qualified=False, unity_parity=False,
                  actual_editor_global_qualified=False, runtime_qualified=False, full_import=False,
                  input_pins=pins, finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    ROOT.mkdir()
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps({k: v for k, v in result.items() if k != 'input_pins'}))


if __name__ == '__main__':
    main()
