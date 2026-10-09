"""Reopen exact resource identities; retain the earlier render-pass refusal."""
import copy
import datetime
import hashlib
import json
import re
from pathlib import Path

OLD = Path('F:/OpenRPG_ImportWork/unity-dungeon-builtin-dust-identity-v1')
ROOT = Path('F:/OpenRPG_ImportWork/dust-exact-resource-identity-reopen-v2')


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def load(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def validate(result, guid):
    for key, expected_guid, file_id, native_type in [
        ('material', guid, '2100000', 'UnityEngine.Material'),
        ('shader', '0000000000000000f000000000000000', '211', 'UnityEngine.Shader'),
        ('main_texture', '0000000000000000f000000000000000', '10300', 'UnityEngine.Texture2D'),
    ]:
        item = result[key]
        if (item['guid'], item['file_id'], item['native_type']) != (expected_guid, file_id, native_type):
            raise ValueError('Exact resource identity differs: ' + key)
        if not isinstance(item['name'], str) or not item['name']:
            raise ValueError('Resource name absent')
        if key != 'material' and item['asset_path'] != 'Resources/unity_builtin_extra':
            raise ValueError('Builtin resource path differs')
    props = result['properties']
    if not props or len({p['name'] for p in props}) != len(props):
        raise ValueError('Duplicate or absent property inventory')
    main = [p for p in props if p['name'] == '_MainTex']
    if len(main) != 1 or main[0]['type'] != 'Texture' or main[0]['texture'] != result['main_texture']:
        raise ValueError('Exact main texture binding differs')
    for key in ['texture_width', 'texture_height', 'texture_mipmap_count']:
        if type(result[key]) is not int or result[key] <= 0:
            raise ValueError('Invalid observed texture dimension')
    if not isinstance(result['passes'], list) or not result['passes'] or not all(
        isinstance(value, str) for value in result['passes']
    ):
        raise ValueError('Observed pass list missing or malformed')
    # Names are preserved observations, never evidence of compiled passes or render parity.


def main():
    assert not ROOT.exists(), 'Retain previous receipt'
    prep = load(OLD / 'PREPARED.v1.json')
    result = load(OLD / 'IDENTITY.v1.json')
    previous = load(OLD / 'RUN-RESULT.v1.json')
    assert previous['unity_exit_code'] == 0 and previous['verifier_exit_code'] == 1
    assert previous['native_identity_verified'] is False
    pins = {str(OLD / name): sha(OLD / name) for name in
            ['PREPARED.v1.json', 'IDENTITY.v1.json', 'RUN-RESULT.v1.json', 'reopen.log', 'verify_identity.py']}
    pins[str(Path(__file__).resolve())] = sha(__file__)
    for key, expected in [('source', prep['source_sha256']),
                          ('copy', prep['source_sha256']),
                          ('source_meta', prep['source_meta_sha256']),
                          ('copy_meta', prep['source_meta_sha256']),
                          ('helper', prep['helper_sha256'])]:
        assert sha(prep[key]) == expected, key
        pins[prep[key]] = expected
    assert sha(OLD / 'manifest.json') == prep['manifest_sha256']
    pins[str(OLD / 'manifest.json')] = prep['manifest_sha256']
    source = Path(prep['source']).read_text(encoding='utf-8-sig')
    meta = Path(prep['source_meta']).read_text(encoding='utf-8-sig')
    assert re.search(r'^guid:\s*' + re.escape(prep['material_guid']) + r'\s*$', meta, re.M)
    assert re.search(r'm_Shader:\s*\{fileID:\s*211,\s*guid:\s*0000000000000000f000000000000000,\s*type:\s*0\}', source)
    assert re.search(r'-\s*_MainTex:\s*\n\s*m_Texture:\s*\{fileID:\s*10300,\s*guid:\s*0000000000000000f000000000000000,\s*type:\s*0\}', source)
    validate(result, prep['material_guid'])
    controls = []
    for key in ['material', 'shader', 'main_texture', 'properties', 'texture_width', 'passes', 'binding']:
        bad = copy.deepcopy(result)
        if key in ['material', 'shader', 'main_texture']:
            bad[key]['file_id'] = '0'
        elif key == 'texture_width':
            bad[key] = 0
        elif key in ['properties', 'passes']:
            bad[key] = []
        else:
            next(p for p in bad['properties'] if p['name'] == '_MainTex')['texture']['file_id'] = '0'
        try:
            validate(bad, prep['material_guid'])
        except ValueError:
            controls.append('refused_' + key)
        else:
            raise AssertionError('Invalid identity accepted: ' + key)
    for path, expected in pins.items():
        assert sha(path) == expected, 'Input changed: ' + path
    output = dict(schema='openrpg.dust-resource-identity-reopen.v2',
                  checked_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  status='exact_source_shader_and_texture_resource_identities_reopened',
                  input_pins=pins, material=result['material'], shader=result['shader'],
                  main_texture=result['main_texture'], properties_observed=len(result['properties']),
                  observed_pass_names=result['passes'], empty_pass_name_indices=[
                      i for i, name in enumerate(result['passes']) if name == ''],
                  texture_dimensions=[result['texture_width'], result['texture_height']],
                  texture_mips=result['texture_mipmap_count'], controls_passed=controls,
                  prior_refusal_preserved=True, source_and_copy_unchanged=True,
                  source_pack_version_qualified=False, passes_compiled_verified=False,
                  texture_pixels_captured=False, gpu_render_parity_qualified=False,
                  runtime_converted=False, studio_verified=False, full_import=False)
    ROOT.mkdir()
    with (ROOT / 'RESULT.v2.json').open('x', encoding='utf-8') as out:
        json.dump(output, out, ensure_ascii=False, allow_nan=False, indent=2)
    print(json.dumps({k: output[k] for k in ['status', 'properties_observed',
                                           'empty_pass_name_indices', 'controls_passed', 'full_import']}))


if __name__ == '__main__':
    main()
