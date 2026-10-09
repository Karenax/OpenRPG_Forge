"""Reopen API capture separately and report original/native state differences."""
from pathlib import Path
import copy
import datetime
import json
import math
import struct
import sys

from verify_native_urp_material_snapshot import reconcile, sha, controls

sys.path.insert(0, 'F:/OpenRPG_AssetVault/cache/skill-dev-python')
import yaml

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'unity-delta-foliage-native-materials-v2'
CONTRACT = W / 'delta-foliage-render-contracts-v1/RESULT.v1.json'


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def f32(value):
    assert type(value) in (int, float) and math.isfinite(value)
    return struct.unpack('<f', struct.pack('<f', value))[0]


def table(entries):
    result = {}
    for entry in entries:
        assert set(entry) == {'first', 'second'} and entry['first'] not in result
        result[entry['first']] = entry['second']
    return result


def save(name, value):
    with (ROOT / name).open('x', encoding='utf-8') as stream:
        json.dump(value, stream, indent=2, allow_nan=False)


def main():
    assert not (ROOT / 'INDEPENDENT-REOPEN.v1.json').exists()
    assert not (ROOT / 'ORIGINAL-NATIVE-STATE-COMPARISON.v1.json').exists()
    paths = [ROOT / name for name in ['RUN-RESULT.v1.json', 'PREPARED.v1.json',
                                    'manifest.v1.json', 'NATIVE-RESULT.v1.json']]
    run, prepared, manifest, native = [read(path) for path in paths]
    assert run['actual_unity_exit_code'] == 0 and run['native_materials_verified'] == 6
    assert sha(paths[-1]) == run['native_result_sha256']
    contracts = read(CONTRACT)
    pins = dict(prepared['input_pins'])
    pins.update(contracts['input_pins'])
    for path in paths + [CONTRACT, Path(__file__).resolve(),
                         Path(__file__).with_name('verify_native_urp_material_snapshot.py')]:
        pins[str(path)] = sha(path)
    assert all(sha(path) == digest for path, digest in pins.items())
    verified = reconcile(manifest, native)
    assert len(verified) == 6 and sum(len(row['required_textures']) for row in manifest['cases']) == 18
    refused = []
    for name in ['wrong_shader', 'uncompiled_pass', 'missing_material', 'wrong_texture']:
        changed = copy.deepcopy(native)
        if name == 'wrong_shader':
            changed['cases'][0]['shader_guid'] = '0' * 32
        elif name == 'uncompiled_pass':
            changed['cases'][0]['passes'][0]['compiled'] = False
        elif name == 'missing_material':
            changed['cases'].pop()
        else:
            prop_name = manifest['cases'][0]['required_textures'][0]['property']
            prop = next(row for row in changed['cases'][0]['properties'] if row['name'] == prop_name)
            prop['texture_guid'] = '0' * 32
        try:
            reconcile(manifest, changed)
        except ValueError as error:
            refused.append(dict(test=name, reason=str(error)))
        else:
            raise AssertionError('Changed real capture accepted: ' + name)
    by_guid = {row['material_guid']: row for row in contracts['rows']}
    comparisons = []
    for row in native['cases']:
        source = by_guid[row['material_guid']]
        original_text = Path(source['original_material']).read_text('utf-8-sig')
        # Remove Unity YAML directives/document tags, never mutate source bytes.
        import re
        original_text = re.sub(r'^%.*\n', '', original_text, flags=re.M)
        original_text = re.sub(r'^--- !u!\d+ &-?\d+.*$', '---', original_text, flags=re.M)
        docs = [doc['Material'] for doc in yaml.safe_load_all(original_text)
                if isinstance(doc, dict) and 'Material' in doc]
        assert len(docs) == 1
        original = docs[0]
        assert original['m_SavedProperties'] == source['source_saved_properties']
        serialized = json.loads(row['native_material_json'])['Material']
        saved = serialized['m_SavedProperties']
        floats, colors = table(saved['m_Floats']), table(saved['m_Colors'])
        differences = []
        for name, value in source['source_floats'].items():
            if name not in floats or f32(floats[name]) != f32(value):
                differences.append(dict(kind='saved_float', property=name, original=value, native=floats.get(name)))
        for name, value in source['source_colors'].items():
            if name not in colors or any(f32(colors[name][axis]) != f32(value[axis]) for axis in 'rgba'):
                differences.append(dict(kind='saved_color', property=name, original=value, native=colors.get(name)))
        keywords = set(row['keywords'])
        flags = {key: ((keyword in keywords) == source[key]) for key, keyword in [
            ('source_keyword_alpha_test', '_ALPHATEST_ON'), ('source_keyword_alpha_to_mask', '_ALPHATOMASK_ON'),
            ('source_keyword_double_sided', '_DOUBLESIDED_ON'), ('source_keyword_transparent', '_SURFACE_TYPE_TRANSPARENT')]}
        assert all(flags.values())
        disabled = set(source['source_disabled_shader_passes'])
        pass_mismatches = [p for p in row['passes'] if p['enabled'] != (p['name'] not in disabled)]
        comparisons.append(dict(material_guid=row['material_guid'], float_comparisons=len(source['source_floats']),
            color_comparisons=len(source['source_colors']), value_differences=differences,
            added_native_keywords=sorted(keywords - set(source['source_valid_keywords'])),
            missing_original_keywords=sorted(set(source['source_valid_keywords']) - keywords),
            core_render_flags_match=flags, source_render_queue=source['source_custom_render_queue'],
            native_render_queue=row['render_queue'], render_queue_matches=row['render_queue'] == source['source_custom_render_queue'],
            disabled_pass_mismatches=pass_mismatches, compiler_messages=row['messages'],
            original_package_version_qualified=False, full_render_state_parity_qualified=False))
    assert all(sha(path) == digest for path, digest in pins.items())
    common = dict(observed_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  original_sources_unchanged=True, input_pins=pins, gpu_qualified=False,
                  studio_verified=False, renderer_installed=False, full_import=False)
    save('ORIGINAL-NATIVE-STATE-COMPARISON.v1.json', dict(common,
         status='six_original_native_state_comparisons_preserved_with_differences', rows=comparisons,
         materials_compared=6, original_render_state_parity_qualified=False))
    save('INDEPENDENT-REOPEN.v1.json', dict(common, status='six_foliage_materials_and_eighteen_bindings_independently_reopened',
         native_materials_verified=6, texture_bindings_verified=18, rows=verified,
         real_capture_refusals=refused, verifier_fixture_controls=controls(),
         native_result_sha256=sha(paths[-1]), state_comparison_sha256=sha(ROOT / 'ORIGINAL-NATIVE-STATE-COMPARISON.v1.json')))
    print(json.dumps(dict(materials_reopened=6, texture_bindings_reopened=18,
                         state_value_differences=sum(len(r['value_differences']) for r in comparisons), full_import=False)))


if __name__ == '__main__':
    main()
