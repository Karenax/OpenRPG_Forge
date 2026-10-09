"""Reopen a capture refused after Unity normalized its private version file."""
import hashlib
import json
from pathlib import Path

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'villa-hierarchy-version-normalization-recovery-v1'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def main():
    assert not ROOT.exists(), 'Previous recovery preserved'
    prepared_path = W / 'villa-native-prefab-hierarchy-project-v1/PREPARED.v1.json'
    prepared = read(prepared_path)
    old = W / 'villa-native-prefab-hierarchy-capture-v1/RUN-RESULT.v1.json'
    assert read(old)['error'] == 'Prepared helper/request/project setting changed'
    version = Path(prepared['project']) / 'ProjectSettings/ProjectVersion.txt'
    before = b'm_EditorVersion: 6000.5.8f1\r\n'
    assert hashlib.sha256(before).hexdigest() == prepared['input_pins'][str(version)]
    assert version.read_text('utf-8').splitlines() == [
        'm_EditorVersion: 6000.5.8f1',
        'm_EditorVersionWithRevision: 6000.5.8f1 (5cb7df797b7d)']
    pins = {str(p): sha(p) for p in [prepared_path, old, version, Path(__file__)]}
    metadata = []
    for row in prepared['copies']:
        assert sha(row['source']) == row['source_sha256'], 'Original source changed'
        pins[row['source']] = row['source_sha256']
        actual = sha(row['private_copy'])
        if actual != row['private_copy_sha256']:
            assert Path(row['private_copy']).suffix.lower() == '.meta', 'Private content changed'
            metadata.append(dict(path=row['private_copy'], before=row['private_copy_sha256'], after=actual))
        pins[row['private_copy']] = actual
    for path, expected in prepared['input_pins'].items():
        if Path(path) != version:
            assert sha(path) == expected, 'Other prepared input changed'
            pins[path] = expected
    queue = W / 'villa-native-prefab-hierarchy-capture-v1/PREPARED.v1.json'
    for path, expected in read(queue)['input_pins'].items():
        assert sha(path) == expected, 'Capture controller input changed'
        pins[path] = expected
    capture = Path(prepared['capture_output'])
    native = read(capture)
    assert native['total'] == native['captured'] == len(native['cases']) == 76
    assert native['failed'] == 0
    expected = {r['asset_id']: r for r in read(prepared['requests'])['cases']}
    assert {r['asset_id'] for r in native['cases']} == set(expected)
    for case in native['cases']:
        assert not case['error'] and case['root']['exact_asset_identity_available']
        assert case['root']['guid'] == expected[case['asset_id']]['expected_guid']
    log = W / 'villa-native-prefab-hierarchy-capture-v1/unity.log'
    assert 'Application will terminate with return code 0' in log.read_text('utf-8', errors='replace')
    pins[str(capture)] = sha(capture)
    pins[str(log)] = sha(log)
    assert all(sha(p) == expected_sha for p, expected_sha in pins.items())
    ROOT.mkdir()
    (ROOT / 'VERSION-PREIMAGE.txt').write_bytes(before)
    result = dict(status='76_native_hierarchies_reopened_after_documented_version_normalization',
                  native_prefab_roots=76, original_source_files_verified=len(prepared['copies']),
                  version_before_sha256=hashlib.sha256(before).hexdigest(),
                  version_after_sha256=sha(version), private_meta_changes=metadata,
                  input_pins=pins, old_refusal_preserved=True, unity_exit_zero_observed_in_log=True,
                  gpu_rendered=False, studio_verified=False, full_import=False)
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps({k: v for k, v in result.items() if k not in ('input_pins', 'private_meta_changes')}))


if __name__ == '__main__':
    main()
