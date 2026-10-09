"""Reopen actual Unity sampling context while preserving reference captures."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import math

W = Path('F:/OpenRPG_ImportWork')
CAP = W / 'firepit-source-sampling-context-v1'
ROOT = W / 'firepit-source-sampling-context-reopen-v1'

def read(path):
    return json.loads(path.read_text('utf-8-sig'))

def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def main():
    assert not ROOT.exists(), 'immutable evidence already exists'
    run = read(CAP / 'RUN-RESULT.v1.json')
    prep = read(CAP / 'PREPARED.v1.json')
    assert run['actual_exit_code'] == 0
    assert run['source_inputs_unchanged'] and not run['private_changed']
    assert len(prep['dependency_paths']) == 24
    for path, expected in prep['input_pins'].items():
        assert sha(Path(path)) == expected, path
    files = [Path(__file__).resolve(), CAP / 'RUN-RESULT.v1.json', CAP / 'PREPARED.v1.json', CAP / 'NATIVE-RESULT.v1.json']
    rows = []
    for view in range(2):
        state_path = CAP / f'SOURCE-STATE-{view}.json'
        state = read(state_path)
        assert state['source_history_channel'] == 'CustomUserHistory0'
        context = json.loads(state['sampling_context_json'])
        assert math.isfinite(context['global_mip_bias'])
        assert context['global_mip_bias_pow2'] == 2 ** context['global_mip_bias']
        assert context['color_buffer_format']
        assert len(context['renderer_property_blocks']) == 4
        assert context['textures']
        for texture in context['textures']:
            assert texture['texture_path'] and texture['material'] and texture['property']
            assert texture['width'] > 0 and texture['height'] > 0 and texture['mips'] > 0
            assert math.isfinite(texture['mip_bias'])
        for relative in [f'images/{view}.png', f'linear-{view}.rgba16f']:
            current = CAP / relative
            reference = W / 'firepit-shadow-source-reference-v8' / relative
            assert sha(current) == sha(reference), relative
            files.extend([current, reference])
        files.append(state_path)
        rows.append(context)
    assert rows[0] == rows[1]
    result = dict(
        status='source_sampling_context_and_unchanged_reference_images_verified',
        finished_utc=datetime.now(timezone.utc).isoformat(),
        views_verified=2, source_dependencies_verified=24,
        reference_images_byte_identical=True, sampling_context=rows[0],
        texture_sampler_rows_verified=len(rows[0]['textures']),
        implicit_texture_gradient_parity_qualified=False,
        color_buffer_encoding_parity_qualified=False,
        render_fidelity_qualified=False, full_import=False,
        complete_assets_delivered_increment=0, vault_assets_updated=0,
        input_pins={str(p): sha(p) for p in files},
        next_action='Compare the measured packed HDR color encoding and implicit texture gradients; retain unchanged thresholds and source material values.')
    ROOT.mkdir()
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps({k: v for k, v in result.items() if k not in ['input_pins', 'sampling_context']}))

if __name__ == '__main__':
    main()
