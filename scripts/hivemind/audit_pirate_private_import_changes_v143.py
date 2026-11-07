"""Rehash every frozen input; retain exact diffs for private SDK import changes."""
from pathlib import Path
from datetime import datetime, timezone
import difflib
import hashlib
import json

W = Path('F:/OpenRPG_ImportWork')
CAP = W/'pirate-source-thumbnail-batch-0105-0112-v1'
PROJECT = W/'unity-library-source-thumbnail-pirate-bench-v2/project'
O = W/'pirate-private-import-change-audit-v143'


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


assert not O.exists()
run = read(CAP/'RUN-RESULT.v1.json')
assert run['actual_exit_code'] == 0
assert run['status'] == 'house_thumbnail_batch_refusal_retained'
prep = read(CAP/'PREPARED.v1.json')
epoch = read(CAP/'PRIVATE-EPOCH.v1.json')
snapshots = {row['live_path']: row for row in epoch['private_input_snapshots']}
changes = []
pins = {}
for name, expected in prep['input_pins'].items():
    path = Path(name)
    actual = sha(path)
    if actual != expected:
        assert path.resolve().is_relative_to(PROJECT.resolve()), 'Original input changed: '+name
        snapshot = snapshots[name]
        original = Path(snapshot['snapshot'])
        assert snapshot['sha256'] == expected == sha(original)
        assert path.suffix in {'.mat', '.asset', '.meta', '.json'}, 'Unexpected private change: '+name
        before = original.read_text('utf-8-sig')
        after = path.read_text('utf-8-sig')
        changes.append(dict(path=name, prior_sha256=expected, current_sha256=actual,
                            original_path=str(original), extension=path.suffix,
                            diff=''.join(difflib.unified_diff(before.splitlines(True),
                                      after.splitlines(True), fromfile='retained-before',
                                      tofile='native-private-after'))))
    pins[name] = actual
for path in [Path(__file__).resolve(), CAP/'PREPARED.v1.json', CAP/'PRIVATE-EPOCH.v1.json',
             CAP/'RUN-RESULT.v1.json', CAP/'NATIVE-RESULT.v1.json']:
    pins[str(path)] = sha(path)
assert len(prep['cases']) == 8
result = dict(status='all_capture_input_pins_rehashed_private_changes_retained_for_classification',
              lot='L08.IMAGE.PIRATE.06', changes=changes, input_pins=pins,
              original_sources_unchanged=True, private_project_byte_unchanged=not changes,
              private_changes_classified=False, full_import=False,
              finished_utc=datetime.now(timezone.utc).isoformat())
O.mkdir()
(O/'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
print(json.dumps(dict(pins_rehashed=len(prep['input_pins']), changes=changes)))
