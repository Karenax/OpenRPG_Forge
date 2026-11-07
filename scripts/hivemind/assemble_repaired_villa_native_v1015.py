"""Continue measured Villa derivatives using existing storage and captured-frame converters."""
import copy
import json
from pathlib import Path

import assemble_native_villa_delivery as assembly
from bind_native_villa_source_frames import convert as bind_frame
from repair_villa_builtin_derivatives_v1014 import SOURCE_PROGRAM, SOURCE_MATERIAL

W = assembly.W
O = W / 'villa-repaired-native-storage-and-frames-v1015'
PRIOR = W / 'villa-builtin-decoder-derivatives-v1014/RESULT.v1.json'


def main():
    require, read, sha = assembly.require, assembly.read, assembly.sha
    require(not O.exists(), 'Existing lot preserved')
    prior = read(PRIOR)
    require(prior['status'] == '19_MEASURED_DECODER_DERIVATIVES_REOPENED' and
            len(prior['rows']) == prior['verified_derivatives'] == 19, 'Decoder lot incomplete')
    textures, plan = read(assembly.TEXTURES), read(assembly.NORMALS)
    require(textures['verified_textures'] == 44 and textures['verified_mips'] == 528, 'Native texture closure incomplete')
    parity = read(assembly.PROBE / 'DECODER-PARITY.v1.json')
    require(parity['candidate_mode2_matches_float_rgorag_gpu_reference'] and
            parity['all_match_builtin_unpack_reference'], 'Texture normal parity missing')
    pins = dict(textures['input_pins'])
    pins.update(plan['input_pins'])
    pins.update(parity['proof_pins'])
    for p in (PRIOR, assembly.TEXTURES, assembly.NORMALS, assembly.PROBE / 'DECODER-PARITY.v1.json',
              assembly.PROBE / 'INPUT.v1.json', Path(__file__).resolve(),
              Path(assembly.__file__), Path(__file__).with_name('apply_scene_normal_bindings.py'),
              Path(__file__).with_name('bind_native_villa_source_frames.py')):
        pins[str(p)] = sha(p)
    for p, expected in pins.items():
        require(sha(p) == expected, 'Historical/native proof input changed: ' + p)
    passing = {r['asset_id'] for r in parity['rows'] if
               r['matches_float_rgorag_reference'] and r['matches_builtin_unpack_reference']}
    normals = {}
    for r in read(assembly.PROBE / 'INPUT.v1.json')['rows']:
        if r['asset_id'] in passing:
            for field in ('original_png', 'original_meta', 'storage'):
                require(sha(r[field]) == r[field + '_sha256'], 'Measured normal source changed')
                pins[r[field]] = r[field + '_sha256']
            normals[r['original_meta_sha256']] = r
    source_map = {r['asset_id']: r for r in plan['rows']}
    texture_map = {r['original_meta_sha256']: r for r in textures['rows']}
    planned = []
    for derivative in prior['rows']:
        row = copy.deepcopy(source_map[derivative['asset_id']])
        require(row['expected_original_glb_sha256'] == derivative['source_glb_sha256'], 'Source plan identity differs')
        require(sha(derivative['glb']) == derivative['sha256'], 'Decoder derivative changed')
        row['glb'] = derivative['glb']
        row['expected_original_glb_sha256'] = derivative['sha256']
        measured = {e['material_index'] for e in derivative['edits']}
        removed = 0
        for material in row['materials']:
            remaining = []
            for slot in material['normal_slots']:
                if slot['proposed_mode'] is None:
                    require(slot['slot'] == 7 and material['material_index'] in measured,
                            'Unresolved slot without exact builtin repair remains deferred')
                    removed += 1
                else:
                    require(slot['proposed_mode'] == 'rg_or_ag', 'Unqualified texture decoder mode')
                    remaining.append(slot)
            material['normal_slots'] = remaining
        require(removed == len(measured) and removed > 0, 'Builtin plan and repairs do not match')
        planned.append(row)
        pins[derivative['glb']] = derivative['sha256']
    O.mkdir()
    assembly.ROOT = O
    assembly.save('PREPARED.v1.json', dict(created_utc=assembly.now(), input_pins=pins,
                  source_rows=planned, native_storage_only=True, full_import=False))
    result = dict(status='RUNNING', started_utc=assembly.now(), rows=[], expected_scenes=19,
                  registry_modified=False, canonical_reader_accepted=False, full_import=False)
    assembly.save('RESULT.v1.json', result)
    try:
        for row in planned:
            assembled = assembly.convert(row, texture_map, normals)
            framed = bind_frame(assembled, O / 'frame-bound')
            framed['native_storage_receipt'] = assembled
            result['rows'].append(framed)
            assembly.save('RESULT.v1.json', result)
            print(json.dumps({'storage_and_frame_reopened': len(result['rows']), 'expected': 19,
                              'asset_id': row['asset_id']}), flush=True)
        for p, expected in pins.items():
            require(sha(p) == expected, 'Input changed while assembling: ' + p)
        result.update(status='19_NATIVE_STORAGE_AND_CAPTURED_FRAMES_REOPENED', verified_scenes=19,
                      next_action='Current common-checkout canonical reader and native material rendering')
        assembly.save('INPUTS.v1.json', [{k: r[k] for k in ('asset_id', 'glb', 'sha256')} for r in result['rows']])
    except Exception as error:
        result.update(status='FAILED', error=str(error))
        raise
    finally:
        result['finished_utc'] = assembly.now()
        assembly.save('RESULT.v1.json', result)


if __name__ == '__main__':
    main()
