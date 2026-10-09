"""Verify actual request-history diffuse context without claiming SSAO pixels."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json

W = Path('F:/OpenRPG_ImportWork')
CAP = W / 'firepit-source-diffuse-context-v2'
ROOT = W / 'firepit-source-diffuse-context-reopen-v2'

def read(p):
    return json.loads(p.read_text('utf-8-sig'))

def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()

def main():
    assert not ROOT.exists()
    run = read(CAP / 'RUN-RESULT.v1.json')
    prep = read(CAP / 'PREPARED.v1.json')
    native = read(CAP / 'NATIVE-RESULT.v1.json')
    assert run['actual_exit_code'] == 0 and run['source_inputs_unchanged']
    assert not run['private_changed']
    for pins in [prep['input_pins'], prep['private_before_pins'], run['context_files']]:
        for path, digest in pins.items():
            assert sha(path) == digest, path
    original = read(W / 'firepit-shadow-source-reference-v8/NATIVE-RESULT.v1.json')
    assert [r['raw_sha256'] for r in native['images']] == [r['raw_sha256'] for r in original['images']]
    states = [read(CAP / f'SOURCE-STATE-{v}.json') for v in range(2)]
    assert states[0] == states[1]
    state = states[0]
    assert state['source_history_channel'] == 'CustomUserHistory0'
    assert state['effective_lit_shader_mode'] == 'Deferred' and state['ssao_enabled']
    volume = json.loads(state['ambient_occlusion_volume'])['MonoBehaviour']
    assert volume['active'] and volume['intensity']['m_Value'] == 0.5
    assert volume['directLightingStrength']['m_Value'] == 0
    pins = [Path(__file__).resolve(), CAP / 'RUN-RESULT.v1.json',
            CAP / 'PREPARED.v1.json', CAP / 'NATIVE-RESULT.v1.json', CAP / 'Unity.log',
            *[CAP / f'SOURCE-STATE-{v}.json' for v in range(2)]]
    result = dict(status='actual_deferred_camera_and_ssao_configuration_verified',
        finished_utc=datetime.now(timezone.utc).isoformat(),
        source_states_verified=2, source_dependencies_verified=len(prep['dependency_paths']),
        original_source_pair_unchanged=True, effective_lit_shader_mode='Deferred',
        ssao_frame_setting_enabled=True, ssao_volume_active=True,
        ssao_configured_intensity=0.5, ssao_direct_lighting_strength=0,
        ssao_pixels_captured=False, combined_diffuse_lighting_qualified=False,
        render_fidelity_qualified=False, editor_visual_verified=False, full_import=False,
        input_pins={str(p): sha(p) for p in pins},
        next_action='Capture actual screen-space AO pixels before applying deferred diffuse lighting; configured SSAO cannot be assumed neutral.')
    ROOT.mkdir()
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps({k: v for k, v in result.items() if k != 'input_pins'}))

if __name__ == '__main__':
    main()
