"""Compare actual source GPU pixels with raw/linear tint hypotheses."""
from pathlib import Path
import datetime
import hashlib
import json
import math

W = Path('F:/OpenRPG_ImportWork')
SOURCE = W / 'unity-delta-firepit-legacy-shader-gpu-builtin-v2'
ROOT = W / 'delta-firepit-native-tint-gpu-v3'
TOLERANCE = 1e-5

def read(p):
    return json.loads(p.read_text('utf-8-sig'))

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def linear(v):
    assert 0 <= v <= 1, 'source fixture tint outside measured range'
    return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4

def main():
    assert not ROOT.exists(), 'retain previous proof'
    status = read(SOURCE / 'RUN-RESULT.v1.json')
    assert status.get('native_shader_fixture_gpu_verified') and status['unity_exit_code'] == 0
    receipt = SOURCE / 'INDEPENDENT-REOPEN.v1.json'
    assert sha(receipt) == status['independent_receipt_sha256']
    proof = read(receipt)
    capture_path = SOURCE / 'GPU.v1.json'
    assert sha(capture_path) == proof['capture_sha256']
    pins = dict(proof['input_pins'])
    for p in (receipt, capture_path, SOURCE / 'RUN-RESULT.v1.json', Path(__file__).resolve()):
        pins[str(p)] = sha(p)
    for p, expected in pins.items():
        assert sha(Path(p)) == expected, 'changed input: ' + p
    capture = read(capture_path)
    assert capture['measurement_render_pipeline'] == 'Builtin' and capture['pipeline_settings_restored']
    comparisons = []
    for case in capture['cases']:
        tint = case['get_color_tint']
        for row in case['rows']:
            vertex = {k: v / 255 for k, v in row['baked_color'].items()}
            alpha = min(max(2 * vertex['a'] * tint['a'], 0), 1)
            expected = {}
            errors = {}
            for name, transform in [('raw_get_color', lambda v: v), ('linearized_get_color', linear)]:
                rgb = {k: 2 * vertex[k] * transform(tint[k]) * alpha + row['background'][k] * (1 - alpha) for k in 'rgb'}
                expected[name] = dict(**rgb, a=row['background']['a'])
                errors[name] = max(abs(row['result'][k] - rgb[k]) for k in 'rgb')
            assert row['result']['a'] == row['background']['a'] and row['alpha_channel_preserved']
            comparisons.append(dict(material_guid=case['material_guid'], native=row,
                expected=expected, maximum_rgb_errors=errors))
    assert len(comparisons) == 16
    maxima = {name: max(row['maximum_rgb_errors'][name] for row in comparisons)
              for name in ['raw_get_color', 'linearized_get_color']}
    admitted = [name for name, error in maxima.items() if error <= TOLERANCE]
    for p, expected in pins.items():
        assert sha(Path(p)) == expected, 'changed input after comparison: ' + p
    ROOT.mkdir()
    result = dict(status='sixteen_original_shader_pixels_compared_with_tint_hypotheses',
        comparisons=comparisons, maximum_rgb_errors=maxima, hypothesis_tolerance=TOLERANCE,
        matching_hypotheses=admitted, unique_uniform_tint_hypothesis_qualified=len(admitted) == 1,
        destination_alpha_preserved=True, input_pins=pins,
        fixture_texture_is_white_and_linear=True, actual_source_texture_sampling_qualified=False,
        soft_particle_and_fog_variants_qualified=False, forge_gpu_parity_qualified=False,
        renderer_installed=False, whole_effect_qualified=False, full_import=False,
        finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps({k: result[k] for k in ['status', 'maximum_rgb_errors', 'matching_hypotheses',
        'unique_uniform_tint_hypothesis_qualified', 'full_import']}))

if __name__ == '__main__':
    main()
