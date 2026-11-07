"""Adjudicate only Unity's regenerated, unused URP settings reference."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import re

W = Path('F:/OpenRPG_ImportWork')
CAP = W / 'pirate-source-thumbnail-batch-0001-0024-v2'
ROOT = W / 'pirate-source-thumbnail-private-settings-reopen-v1'
PROJECT = W / 'unity-library-source-thumbnail-pirate-bench-v2/project'
TEMPLATE = W / 'unity-library-source-thumbnail-house-bench-v2/project'


def read(p):
    return json.loads(Path(p).read_text(encoding='utf-8-sig'))


def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()


assert not ROOT.exists()
run, prep, batch = [read(CAP / name) for name in ['RUN-RESULT.v1.json', 'PREPARED.v1.json', 'NATIVE-RESULT.v1.json']]
assert run['actual_exit_code'] == 0 and run['status'] == 'house_thumbnail_batch_refusal_retained'
assert len(batch['cases']) == len(prep['cases']) == 24 and all(c['captured'] for c in batch['cases'])
old, current = TEMPLATE / 'ProjectSettings/GraphicsSettings.asset', PROJECT / 'ProjectSettings/GraphicsSettings.asset'
assert sha(old) == prep['input_pins'][str(current)]
before, after = old.read_text(encoding='utf-8'), current.read_text(encoding='utf-8')
pattern = r'(UnityEngine.Rendering.Universal.UniversalRenderPipeline: \{fileID: 11400000, guid: )([a-f0-9]{32})(, type: 2\})'
a, b = re.search(pattern, before), re.search(pattern, after)
assert a and b and a.group(2) != b.group(2)
assert after == before[:a.start(2)] + b.group(2) + before[a.end(2):]
pins, changes = {}, []
for name, expected in prep['input_pins'].items():
    p = Path(name)
    actual = sha(p)
    if actual != expected:
        assert p == current, p
        changes.append(dict(path=name, prior_sha256=expected, current_sha256=actual,
                            prior_exact_snapshot=str(old), only_changed_field='unused URP global settings reference'))
    pins[name] = actual
assert len(changes) == 1
for case, request in zip(batch['cases'], prep['cases']):
    assert case['id'] == request['id']
    p = Path(case['receipt'])
    native = read(p)
    assert native['id'] == case['id'] and native['prefab_guid'] == request['prefab_guid']
    assert native['pipeline'] == 'UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset'
    assert native['source_object_unchanged'] and native['preview_scene_isolated']
    assert len(native['images']) == 2
    pins[str(p)] = sha(p)
    for image in native['images']:
        assert sha(image['path']) == image['sha256']
        pins[image['path']] = image['sha256']
for p in [Path(__file__), old, CAP / 'PREPARED.v1.json', CAP / 'RUN-RESULT.v1.json', CAP / 'NATIVE-RESULT.v1.json']:
    pins[str(p)] = sha(p)
ROOT.mkdir()
result = dict(status='captured_hdrp_source_cards_reopened_private_urp_reference_change_adjudicated',
    finished_utc=datetime.now(timezone.utc).isoformat(), lot='L08.IMAGE.PIRATE.01', cards_captured=24,
    images_captured=48, source_inputs_unchanged=True, actual_hdrp_pipeline_verified_for_every_case=True,
    private_settings_changes=changes, input_pins=pins, original_refusal_retained=True,
    visual_review_qualified=False, images_installed_in_vault=False, full_import=False)
(ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps({k: v for k, v in result.items() if k != 'input_pins'}))
