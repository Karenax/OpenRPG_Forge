"""Verify original native images and inputs; visual acceptance is separate."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, struct

W = Path('F:/OpenRPG_ImportWork')
CAP = W / 'house-cliff-lux-capture-v737'
OUT = W / 'house-cliff-lux-image-check-v738'

def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def main():
    assert not OUT.exists(), 'Preserve existing evidence'
    state = read(CAP / 'RUN-RESULT.v1.json')
    assert state['status'] == 'house_native_thumbnail_batch_captured_review_pending', state
    assert state['actual_exit_code'] == 0 and state['source_inputs_unchanged']
    prep = read(CAP / 'PREPARED.v1.json')
    assert prep['expected_cards'] == 2 and prep['expected_images'] == 4
    pins = dict(prep['input_pins'])
    for path, digest in pins.items():
        assert sha(path) == digest, path
    report = read(CAP / 'NATIVE-RESULT.v1.json')
    assert [case['id'] for case in report['cases']] == [case['id'] for case in prep['cases']]
    rows = []
    for case in report['cases']:
        assert case['captured'], case
        path = Path(case['receipt'])
        assert path.resolve().is_relative_to(CAP.resolve())
        native = read(path)
        assert native['id'] == case['id'] and native['source_object_unchanged']
        assert native['preview_scene_isolated'] and native['presentation_only']
        assert native['gpu'] != 'Null' and native['presentation_fixed_ev'] == 8
        assert native['presentation_tonemapping'] == 'Neutral'
        assert native['actual_directional_light_intensity'] == 10000
        assert native['compiled_passes'] and all(p['compiled'] for p in native['compiled_passes'])
        assert len(native['images']) == 2
        pins[str(path)] = sha(path)
        for image in native['images']:
            image_path = Path(image['path'])
            assert image_path.resolve().parent == (path.parent / 'images').resolve()
            data = image_path.read_bytes()
            assert data[:8] == b'\x89PNG\r\n\x1a\n' and data[12:16] == b'IHDR'
            assert struct.unpack('>II', data[16:24]) == (512, 512)
            assert sha(image_path) == image['sha256']
            assert image['clipped_rgb_pixels'] == 0 and image['linear_max_channel'] <= 1
            pins[str(image_path)] = image['sha256']
        rows.append(dict(id=case['id'], native_receipt=str(path),
                         native_receipt_sha256=pins[str(path)], images=native['images']))
    for path in [Path(__file__), CAP / 'PREPARED.v1.json', CAP / 'RUN-RESULT.v1.json', CAP / 'NATIVE-RESULT.v1.json']:
        pins[str(path)] = sha(path)
    for path, digest in pins.items():
        assert sha(path) == digest, path
    result = dict(status='two_cliff_native_image_pairs_technically_verified_visual_review_pending',
                  utc=datetime.now(timezone.utc).isoformat(), cards_verified=2,
                  images_verified=4, rows=rows, input_pins=pins,
                  source_inputs_unchanged=True, presentation_only=True,
                  agent_visual_review=False, human_review=False,
                  images_installed_in_vault=False, studio_visual_verified=False,
                  full_import=False, full_import_increment=0)
    OUT.mkdir()
    (OUT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps({key: value for key, value in result.items() if key not in ['rows', 'input_pins']}))

if __name__ == '__main__':
    main()
