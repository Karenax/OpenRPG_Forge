"""Independently check both native surface sinks, not aggregate lit coverage."""
from pathlib import Path
import datetime
import hashlib
import json
import math
import struct

ROOT = Path('F:/OpenRPG_ImportWork/unity-source-null-mask-surface-v8')
OUT = ROOT.parent / 'detail-mask-surface-independent-reopen-v1'

def read(path):
    return json.loads(path.read_text('utf-8-sig'))

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    assert not OUT.exists(), 'Retain existing evidence'
    terminal = read(ROOT / 'RUN-RESULT.v1.json')
    assert terminal.get('finished_utc'), 'Native controller still running'
    assert terminal['unity_exit_code'] == 0
    prepared = read(ROOT / 'PREPARED.v1.json')
    report = read(ROOT / 'NATIVE.v1.json')
    OUT.mkdir()
    result = dict(status='checking_both_native_surface_sinks', full_import=False,
        surface_data_only=True, lit_render_qualified=False,
        native_material_migration_qualified=False, whole_prefab_qualified=False,
        native_surface_default_qualified=False, input_pins={}, modes={})
    try:
        assert report['surface_data_only'] and report['debug_material_settings_restored']
        assert report['source_material_unchanged'] and report['source_renderer_unchanged']
        assert len(report['rows']) == 36
        groups = {}
        for row in report['rows']:
            assert row['native_debug_mode_active'] and row['same_hd_request_camera']
            assert row['effective_hd_camera_time'] == 0
            assert row['debug_surface_mode'] == (1005 if row['camera'] < 3 else 1006)
            path = Path(row['path'])
            assert path.resolve().is_relative_to((ROOT / 'native-pixels').resolve())
            raw = path.read_bytes()
            assert len(raw) == 8192 and sha(path) == row['sha256']
            pixels = list(struct.iter_unpack('<4e', raw))
            assert all(math.isfinite(value) for pixel in pixels for value in pixel)
            assert row['variant'] not in groups.setdefault(row['camera'], {})
            groups[row['camera']][row['variant']] = pixels
            result['input_pins'][str(path)] = sha(path)
        assert set(groups) == set(range(6))
        for mode, label in [(1005, 'smoothness'), (1006, 'occlusion')]:
            changed = 0
            maximum = 0.0
            ids = range(3) if mode == 1005 else range(3, 6)
            for camera in ids:
                group = groups[camera]
                assert len(group) == 6
                null = group['original_null']
                assert null == group['original_null_repeat'], 'Native surface repeat differs'
                white = group['explicit_white_control']
                black = group['explicit_black_control']
                maximum = max(maximum, max(abs(a-b) for p,q in zip(null, white, strict=True) for a,b in zip(p,q,strict=True)))
                changed += sum(p != q for p,q in zip(null, black, strict=True))
            result['modes'][label] = dict(native_mode=mode, maximum_null_white_error=maximum,
                black_control_changed_pixels=changed, original_repeat_exact=True)
            assert maximum <= prepared['comparison_tolerance'], label + ' default differs from white'
            assert changed > 0, label + ' has no positive black-control coverage'
        for path, digest in prepared['input_pins'].items():
            assert sha(Path(path)) == digest, 'Changed pinned input: ' + path
        for path in [ROOT/'PREPARED.v1.json', ROOT/'RUN-RESULT.v1.json', ROOT/'NATIVE.v1.json', Path(__file__).resolve()]:
            result['input_pins'][str(path)] = sha(path)
        result.update(status='both_native_surface_sinks_match_white_with_positive_black_controls',
            native_surface_default_qualified=True, images_reopened=36, pixels_reopened=36864,
            source_pins_unchanged=True)
    except Exception as error:
        result.update(status='refused_or_unverified', error=repr(error))
    result['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    temporary = OUT / 'RESULT.v1.json.tmp'
    temporary.write_text(json.dumps(result, indent=2), 'utf-8')
    temporary.replace(OUT / 'RESULT.v1.json')
    print(json.dumps({key:value for key,value in result.items() if key != 'input_pins'}))

if __name__ == '__main__':
    main()
