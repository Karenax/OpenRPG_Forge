"""Distinguish saved material values from effective Shader.GetProperty API data."""
from pathlib import Path
import datetime
import hashlib
import json
import struct

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'unity-delta-foliage-native-materials-v2'


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def f32(value):
    return struct.unpack('<f', struct.pack('<f', value))[0]


def main():
    original_path = W / 'delta-foliage-render-contracts-v1/RESULT.v1.json'
    native_path = ROOT / 'NATIVE-RESULT.v1.json'
    receipt_path = ROOT / 'INDEPENDENT-REOPEN.v1.json'
    saved_path = ROOT / 'ORIGINAL-NATIVE-STATE-COMPARISON.v1.json'
    originals, native, receipt, saved = [read(p) for p in [original_path, native_path, receipt_path, saved_path]]
    assert sha(native_path) == receipt['native_result_sha256']
    assert sha(saved_path) == receipt['state_comparison_sha256']
    pins = dict(receipt['input_pins'])
    for path in [original_path, native_path, receipt_path, saved_path, Path(__file__).resolve()]:
        pins[str(path)] = sha(path)
    assert all(sha(path) == digest for path, digest in pins.items())
    sources = {row['material_guid']: row for row in originals['rows']}
    rows = []
    for material in native['cases']:
        source = sources[material['material_guid']]
        differences, compared = [], []
        for prop in material['properties']:
            name = prop['name']
            if prop['type'] in ['Float', 'Range', 'Int'] and name in source['source_floats']:
                authored, effective = f32(source['source_floats'][name]), f32(prop['native_float'])
            elif prop['type'] in ['Color', 'Vector'] and name in source['source_colors']:
                authored = [f32(source['source_colors'][name][axis]) for axis in 'rgba']
                effective = [f32(prop['native_vector'][axis]) for axis in 'xyzw']
            else:
                continue
            record = dict(property=name, type=prop['type'], original_float32=authored,
                          effective_float32=effective, equal=authored == effective)
            compared.append(record)
            if not record['equal']:
                differences.append(record)
        saved_row = next(r for r in saved['rows'] if r['material_guid'] == material['material_guid'])
        rows.append(dict(material_guid=material['material_guid'], compared_properties=compared,
            effective_property_differences=differences, saved_property_differences=saved_row['value_differences'],
            added_native_keywords=saved_row['added_native_keywords'],
            disabled_pass_differences=saved_row['disabled_pass_mismatches'],
            source_render_queue=source['source_custom_render_queue'], native_render_queue=material['render_queue'],
            original_render_state_parity_qualified=False))
    assert len(rows) == 6 and all(sha(path) == digest for path, digest in pins.items())
    result = dict(schema='hivemind.delta-foliage-effective-state-comparison.v1',
        observed_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        status='effective_and_saved_states_compared_with_unresolved_import_differences',
        materials_compared=6, effective_property_differences=sum(len(r['effective_property_differences']) for r in rows),
        saved_property_differences=sum(len(r['saved_property_differences']) for r in rows),
        rows=rows, input_pins=pins, original_sources_unchanged=True,
        original_authoring_version_qualified=False, original_render_state_parity_qualified=False,
        gpu_qualified=False, studio_verified=False, full_import=False)
    with (ROOT / 'EFFECTIVE-PROPERTY-COMPARISON.v1.json').open('x', encoding='utf-8') as stream:
        json.dump(result, stream, indent=2, allow_nan=False)
    print(json.dumps({k: result[k] for k in ['materials_compared', 'effective_property_differences', 'saved_property_differences', 'full_import']}))


if __name__ == '__main__':
    main()
