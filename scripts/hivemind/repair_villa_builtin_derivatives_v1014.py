"""Create measured decoder derivatives; preserve original GLBs and all original BIN bytes."""
import copy
import json
import os
import subprocess
from pathlib import Path

from assemble_native_villa_delivery import split_glb, pack_glb, read, sha, digest, require, now
from compare_villa_builtin_normal_gpu_v1006 import function

W = Path('F:/OpenRPG_ImportWork')
O = W / 'villa-builtin-decoder-derivatives-v1014'
PROOF = W / 'villa-builtin-decoder-repair-plan-v1010/RESULT.v1.json'
API = W / 'unity-villa-normal-default-api-observation-v3/NATIVE-RESULT.v1.json'
SOURCE_PROGRAM = 'efe016bb38f8eaac225c94f7bbb4676c07c24ef5f18d1bcf85200e978ceeed59'
SOURCE_MATERIAL = 'f0987fb354474961a00a32d6b41e2ff46ffe0aca17f0727a94d7c8647def0f51'
PROOF_SHA = 'ae29c5e0cf4b8b10b78a33c815b0eedf8388afa67e711f994dc4fab7949c480c'
API_SHA = 'a059e10616a23f87420cc57143ea2d6ff5cc93d8fcb5a22f4ed44c651734fe33'


def save(name, value):
    path = O / name
    temporary = path.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(value, indent=2, allow_nan=False), 'utf-8')
    os.replace(temporary, path)


def payload(document, data, descriptor):
    view = document['bufferViews'][descriptor['buffer_view']]
    require(view['buffer'] == 0, 'External shader buffer refused')
    offset = view.get('byteOffset', 0)
    raw = data[offset:offset + view['byteLength']]
    require(len(raw) == view['byteLength'] and digest(raw) == descriptor['sha256'], 'Shader payload SHA mismatch')
    return raw


def convert(row, original_function, repaired_function):
    source = Path(row['glb'])
    raw = source.read_bytes()
    require(digest(raw) == row['sha256'], 'Source GLB changed')
    source_doc, source_bin = split_glb(raw)
    require(source_doc.get('extras', {}).get('unity_captured_vertices'), 'Native vertex evidence absent')
    document, data = copy.deepcopy(source_doc), bytearray(source_bin)
    edits = []
    for index, material in enumerate(document.get('materials', [])):
        contract = material.get('extensions', {}).get('OPENRPG_source_graph')
        if contract is None:
            continue
        require(contract['schema_version'] == 2, 'Unexpected source contract version')
        if contract['source_program_sha256'] != SOURCE_PROGRAM or contract['source_material_sha256'] != SOURCE_MATERIAL:
            continue  # Other source materials remain byte-for-byte unchanged, without qualification.
        require(len(contract['normal_decode_modes']) == 8 and contract['normal_decode_modes'][7] is None,
                'Builtin slot already assigned or ABI changed')
        stages = []
        for stage in ('forward', 'prepass'):
            before = copy.deepcopy(contract[stage])
            text = payload(source_doc, source_bin, before).decode('utf-8')
            actual_function = function(text, 'source_graph_normal_sample')
            canonical = lambda value: '\n'.join(line for line in value.splitlines() if line.strip())
            require(canonical(actual_function) == canonical(original_function),
                    'Shader decoder differs from measured GPU source')
            require(text.count(actual_function) == 1 and
                    'let n6=source_graph_normal_sample(vec4<f32>(1.0,1.0,1.0,1.0),i.builtin_normal_mode_6);' in text and
                    'output.builtin_normal_mode_6=source_graph.normal_decode_modes[1].w;' in text,
                    'Builtin white constant or slot-7 connection changed')
            line_end = '\r\n' if '\r\n' in actual_function else '\n'
            actual_repair = line_end.join(line for line in repaired_function.splitlines() if line.strip())
            changed = text.replace(actual_function, actual_repair, 1)
            require(function(changed, 'source_decode_normal') == function(text, 'source_decode_normal'),
                    'Other normal decoder modes changed')
            encoded = changed.encode('utf-8')
            data.extend(b'\x00' * (-len(data) % 4))
            view_index = len(document['bufferViews'])
            document['bufferViews'].append({'buffer': 0, 'byteOffset': len(data), 'byteLength': len(encoded)})
            data.extend(encoded)
            contract[stage] = {'buffer_view': view_index, 'sha256': digest(encoded)}
            stages.append({'stage': stage, 'before': before, 'after': copy.deepcopy(contract[stage])})
        contract['normal_decode_modes'][7] = 'rg_or_ag'
        edits.append({'material_index': index, 'slot': 7, 'before': None, 'after': 'rg_or_ag', 'stages': stages})
    require(edits, 'No measured source material in derivative')
    document['buffers'][0]['byteLength'] = len(data)
    folder = O / 'jobs' / row['asset_id']
    folder.mkdir(parents=True, exist_ok=False)
    temporary = folder / 'model.glb.unverified'
    temporary.write_bytes(pack_glb(document, bytes(data)))
    reopened_doc, reopened_bin = split_glb(temporary.read_bytes())
    require(reopened_bin[:len(source_bin)] == source_bin, 'Original BIN bytes changed')
    restored = copy.deepcopy(reopened_doc)
    for edit in edits:
        contract = reopened_doc['materials'][edit['material_index']]['extensions']['OPENRPG_source_graph']
        revert = restored['materials'][edit['material_index']]['extensions']['OPENRPG_source_graph']
        require(contract['normal_decode_modes'][7] == 'rg_or_ag', 'Reopened builtin binding differs')
        revert['normal_decode_modes'][7] = edit['before']
        for stage in edit['stages']:
            text = payload(reopened_doc, reopened_bin, contract[stage['stage']]).decode('utf-8')
            original = payload(source_doc, source_bin, stage['before']).decode('utf-8')
            old_function = function(original, 'source_graph_normal_sample')
            new_function = function(text, 'source_graph_normal_sample')
            require(canonical(new_function) == canonical(repaired_function), 'Reopened decoder differs from candidate')
            require(text.replace(new_function, old_function, 1) == original,
                    'Shader changed outside measured decoder function')
            revert[stage['stage']] = stage['before']
    restored['bufferViews'] = restored['bufferViews'][:len(source_doc['bufferViews'])]
    restored['buffers'] = copy.deepcopy(source_doc['buffers'])
    require(restored == source_doc, 'JSON changed outside shader descriptors and builtin mode')
    require(sha(source) == row['sha256'], 'Original source changed during conversion')
    final = folder / 'model.glb'
    os.replace(temporary, final)
    receipt = dict(asset_id=row['asset_id'], glb=str(final), sha256=sha(final), source_glb=str(source),
                   source_glb_sha256=row['sha256'], original_bin_sha256=digest(source_bin), edits=edits,
                   original_geometry_textures_metadata_and_shader_bytes_preserved=True,
                   measured_decoder_function_only=True, builtin_white_api_observed=True,
                   actual_source_draw_binding_qualified=False, canonical_reader_accepted=False,
                   native_texture_storage_embedded=False, studio_verified=False, full_import=False)
    (folder / 'RECEIPT.v1.json').write_text(json.dumps(receipt, indent=2), 'utf-8')
    return receipt


def main():
    require(not O.exists(), 'Existing lot preserved; do not restart')
    require(sha(PROOF) == PROOF_SHA and sha(API) == API_SHA, 'Measured proof changed')
    proof, api = read(PROOF), read(API)
    require(proof['status'] == 'MEASURED_DECODER_REPAIR_CANDIDATE_GPU_PASS' and
            proof['mode2_max_abs_errors'] == [0.0] * 4 and proof['other_modes_bitwise_preserved'],
            'Measured decoder parity absent')
    require(api['normal_default_name'] == 'white' and
            any(r['property'] == '_NormalMap' and r['owner'] == 'source_material' and r['is_null'] for r in api['rows']),
            'Native source default observation absent')
    pins = dict(proof['input_pins'])
    pins.update({str(PROOF): PROOF_SHA, str(API): API_SHA})
    helper = Path(__file__).resolve()
    for p in (helper, helper.with_name('repair_villa_builtin_derivatives_v1013.py'), helper.with_name('repair_villa_builtin_derivatives_v1012.py'), helper.with_name('assemble_native_villa_delivery.py'),
              helper.with_name('compare_villa_builtin_normal_gpu_v1006.py')):
        pins[str(p)] = sha(p)
    for p, expected in pins.items():
        require(sha(p) == expected, 'Pinned proof input changed: ' + p)
    old = function((W / 'villa-builtin-normal-gpu-comparison-v1009/kernel.wgsl').read_text('utf-8'), 'source_graph_normal_sample')
    new = function((W / 'villa-builtin-decoder-repair-plan-v1010/candidate-kernel.wgsl').read_text('utf-8'), 'source_graph_normal_sample')
    rows = proof['source_rows']
    require(len(rows) == len({r['asset_id'] for r in rows}) == 19, 'Expected exact 19-scene scope')
    prior_path = W / 'villa-builtin-decoder-derivatives-v1013/RESULT.v1.json'
    prior = read(prior_path)
    require(prior['status'] == 'FAILED' and len(prior['rows']) == 1, 'Expected one retained reopened derivative')
    retained = {r['asset_id']: r for r in prior['rows']}
    pins[str(prior_path)] = sha(prior_path)
    O.mkdir()
    repo = helper.parents[2]
    inventory = subprocess.check_output(['git', '-C', str(repo), 'ls-files', '-c', '-o', '--exclude-standard', '-z'])
    status = subprocess.check_output(['git', '-C', str(repo), 'status', '--porcelain=v1', '-z'])
    (O / 'checkout-input-paths.zlist').write_bytes(inventory)
    (O / 'checkout-status.zlist').write_bytes(status)
    save('PREPARED.v1.json', dict(created_utc=now(), input_pins=pins, source_rows=rows,
                                 checkout_path_inventory_sha256=digest(inventory), checkout_status_sha256=digest(status),
                                 whole_checkout_qualification=False, full_import=False))
    result = dict(status='RUNNING', started_utc=now(), rows=[], expected_derivatives=19,
                  source_glbs_modified=False, registry_modified=False, full_import=False)
    save('RESULT.v1.json', result)
    try:
        for row in rows:
            if row['asset_id'] in retained:
                previous = retained[row['asset_id']]
                require(previous['source_glb_sha256'] == row['sha256'], 'Retained source identity changed')
                require(sha(Path(previous['glb'])) == previous['sha256'] and sha(Path(row['glb'])) == row['sha256'], 'Retained derivative or source changed')
                sd, sb = split_glb(Path(row['glb']).read_bytes())
                dd, db = split_glb(Path(previous['glb']).read_bytes())
                require(db[:len(sb)] == sb, 'Retained source BIN changed')
                restored = copy.deepcopy(dd)
                for edit in previous['edits']:
                    c = dd['materials'][edit['material_index']]['extensions']['OPENRPG_source_graph']
                    rc = restored['materials'][edit['material_index']]['extensions']['OPENRPG_source_graph']
                    require(c['normal_decode_modes'][7] == 'rg_or_ag', 'Retained builtin mode changed')
                    rc['normal_decode_modes'][7] = edit['before']
                    for stage in edit['stages']:
                        prior = payload(sd, sb, stage['before']).decode('utf-8')
                        current = payload(dd, db, c[stage['stage']]).decode('utf-8')
                        normalize = lambda value: '\n'.join(line for line in value.splitlines() if line.strip())
                        require(normalize(function(current, 'source_graph_normal_sample')) == normalize(new), 'Retained candidate differs')
                        require(current.replace(function(current, 'source_graph_normal_sample'), function(prior, 'source_graph_normal_sample'), 1) == prior, 'Retained shader outside repair changed')
                        rc[stage['stage']] = stage['before']
                restored['buffers'] = sd['buffers']
                restored['bufferViews'] = restored['bufferViews'][:len(sd['bufferViews'])]
                require(restored == sd, 'Retained source JSON changed')
                result['rows'].append(previous)
            else:
                result['rows'].append(convert(row, old, new))
            save('RESULT.v1.json', result)
            print(json.dumps({'reopened': len(result['rows']), 'expected': 19, 'asset_id': row['asset_id']}), flush=True)
        for p, expected in pins.items():
            require(sha(p) == expected, 'Proof changed during conversion: ' + p)
        result.update(status='19_MEASURED_DECODER_DERIVATIVES_REOPENED', verified_derivatives=19,
                      next_action='Embed measured native texture storage and bind captured frames, then current canonical reader')
    except Exception as error:
        result.update(status='FAILED', error=str(error))
        raise
    finally:
        result['finished_utc'] = now()
        save('RESULT.v1.json', result)


if __name__ == '__main__':
    main()
