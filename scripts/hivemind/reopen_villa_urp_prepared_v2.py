"""Independently reopen the completed URP v2 capture and original inputs."""
from pathlib import Path
import datetime
import json
import copy

from verify_native_urp_material_snapshot import controls, urp_controls, reconcile_urp, sha

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'unity-villa-prepared-native-urp-v2'
PLAN = W / 'villa-urp-native-capture-inputs-v1/RESULT.v1.json'


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def main():
    target = ROOT / 'INDEPENDENT-REOPEN.v1.json'
    assert not target.exists()
    run_path, native_path, prepared_path = [ROOT / name for name in ['RUN-RESULT.v2.json', 'NATIVE-RESULT.v2.json', 'PREPARED.v2.json']]
    run, native, prepared, plan = [read(path) for path in [run_path, native_path, prepared_path, PLAN]]
    assert run['status'] == 'six_original_urp_materials_and_ten_defaults_captured'
    assert run['creating_separate_urp_context_exit_code'] == run['capturing_six_prepared_original_urp_materials_exit_code'] == 0
    assert sha(native_path) == run['native_result_sha256']
    pins = dict(prepared['input_pins'])
    pins.update(plan['input_pins'])
    for path in [run_path, native_path, prepared_path, PLAN, Path(__file__).resolve(),
                 Path(__file__).with_name('verify_native_urp_material_snapshot.py')]:
        pins[str(path)] = sha(path)
    assert all(sha(path) == digest for path, digest in pins.items())
    result = reconcile_urp(plan, native)
    assert len(result['materials']) == 6 and len(result['defaults']) == 10
    shared, urp = controls(), urp_controls()
    # Verify that changes to a real request/capture do not pass the independent
    # reconciler; fixture controls alone do not exercise this captured dataset.
    negative = []
    for name in ['missing_material', 'wrong_pipeline', 'shader_identity', 'uncompiled_pass']:
        changed = copy.deepcopy(native)
        if name == 'missing_material':
            changed['cases'].pop()
        elif name == 'wrong_pipeline':
            changed['selected_pipeline_type'] = 'UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset'
        elif name == 'shader_identity':
            changed['cases'][0]['shader_guid'] = '0' * 32
        else:
            changed['cases'][0]['passes'][0]['compiled'] = False
        try:
            reconcile_urp(plan, changed)
        except ValueError as error:
            negative.append(dict(test=name, refused=True, reason=str(error)))
        else:
            raise AssertionError('Malformed native capture accepted: ' + name)
    assert all(sha(path) == digest for path, digest in pins.items())
    evidence = dict(schema='hivemind.villa-urp-native-independent-reopen.v1',
        status='six_urp_materials_and_ten_defaults_independently_reopened',
        observed_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(), native_materials_verified=6,
        native_defaults_verified=10, result=result, shared_controls=shared, urp_controls=urp,
        scoped_real_capture_refusals=negative, input_pins=pins, source_pins_reopened=True,
        native_result_sha256=sha(native_path), original_authoring_version_qualified=False,
        gpu_verified=False, studio_verified=False, full_import=False)
    with target.open('x', encoding='utf-8') as stream:
        json.dump(evidence, stream, indent=2, allow_nan=False)
    print(json.dumps(dict(materials_reopened=6, defaults_reopened=10, real_capture_refusals=len(negative), receipt=str(target))))


if __name__ == '__main__':
    main()
