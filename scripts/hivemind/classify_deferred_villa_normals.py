"""Classify deferred ABI reads without changing shaders or supplying textures."""
import datetime
import hashlib
import json
from pathlib import Path
import re
import struct

WORK = Path('F:/OpenRPG_ImportWork')
ROOT = WORK / 'villa-deferred-builtin-normal-diagnosis-v1'
PLAN = WORK / 'villa-native-scene-normal-bindings-v1/PLAN.v1.json'
PROBE = WORK / 'unity-builtin-normal-constant-reference-v1/RUN-RESULT.v1.json'


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def main():
    assert not ROOT.exists(), 'Existing diagnosis must be preserved'
    plan = json.loads(PLAN.read_text('utf-8-sig'))
    selected = [(row, material, slot) for row in plan['rows'] for material in row['materials']
                for slot in material['normal_slots'] if slot.get('requires_conversion_repair')]
    assert len(selected) == 19
    result = {'schema': 'openrpg.deferred-builtin-normal-diagnosis.v1',
              'started_utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
              'plan_sha256': sha(PLAN), 'helper_sha256': sha(__file__), 'rows': [],
              'native_reference_required': str(PROBE), 'source_shaders_modified': False,
              'glb_modified': False, 'full_glb_hash_reopened_this_diagnosis': False,
              'normal_render_qualified': False, 'studio_verified': False, 'full_import': False}
    for row, material, slot in selected:
        assert slot['slot'] == 7 and slot['reason'] == 'normal_uniform_has_no_matching_texture_slot'
        with Path(row['glb']).open('rb') as stream:
            header = stream.read(20)
            assert header[:4] == b'glTF' and header[16:20] == b'JSON'
            length = struct.unpack_from('<I', header, 12)[0]
            json_bytes = stream.read(length)
            document = json.loads(json_bytes)
            contract = document['materials'][material['material_index']]['extensions']['OPENRPG_source_graph']
            assert len(contract['textures']) == slot['texture_count']
            programs = []
            # Inspect declared shader payloads; do not treat PNG/geometry as shader text.
            def descriptors(value):
                if isinstance(value, dict):
                    if 'buffer_view' in value and 'sha256' in value:
                        yield value
                    else:
                        for child in value.values():
                            yield from descriptors(child)
                elif isinstance(value, list):
                    for child in value:
                        yield from descriptors(child)
            for descriptor in descriptors({'forward': contract['forward'], 'prepass': contract['prepass']}):
                view = document['bufferViews'][descriptor['buffer_view']]
                stream.seek(28 + length + view.get('byteOffset', 0))
                raw = stream.read(view['byteLength'])
                assert hashlib.sha256(raw).hexdigest() == descriptor['sha256'], 'Shader payload hash differs'
                text = raw.decode('utf-8')
                if 'builtin_normal_mode_6' not in text:
                    continue
                assert 'output.builtin_normal_mode_6=source_graph.normal_decode_modes[1].w;' in text
                constant = re.findall(r'source_graph_normal_sample\(vec4<f32>\(([^)]*)\),i\.builtin_normal_mode_6\)', text)
                assert constant and all([float(v) for v in item.split(',')] == [1.0]*4 for item in constant), 'Builtin sample differs'
                programs.append({'buffer_view': descriptor['buffer_view'], 'sha256': descriptor['sha256'],
                                 'constant_rgba': [1.0]*4, 'reserved_decoder_slot': 7})
            assert programs, 'No proven builtin normal expression'
        result['rows'].append({'asset_id': row['asset_id'], 'glb': row['glb'],
            'expected_original_glb_sha256': row['expected_original_glb_sha256'],
            'header_sha256': hashlib.sha256(json_bytes).hexdigest(),
            'material_index': material['material_index'], 'texture_count': slot['texture_count'],
            'shader_payloads_verified': programs,
            'classification': 'builtin_white_constant_normal_decoder_abi_not_missing_image',
            'native_default_decoder_and_gpu_reference_still_required': True})
    assert sha(PLAN) == result['plan_sha256'] and sha(__file__) == result['helper_sha256']
    result.update(status='19_deferred_builtin_constant_reads_identified_render_unqualified',
                  classified_reads=len(result['rows']), finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    ROOT.mkdir()
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k != 'rows'}))


if __name__ == '__main__':
    main()
