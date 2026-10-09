"""Assemble one documented native batch, preserving all existing GLB BIN bytes."""
import copy
import datetime
import hashlib
import json
import os
from pathlib import Path
import struct
import subprocess
import sys

from apply_scene_normal_bindings import change_document

W = Path('F:/OpenRPG_ImportWork')
BATCHES = W / 'SUIVI_LOTS_HIVEMIND/SOUS_LOTS_VILLA.v1.json'
TEXTURES = W / 'villa-native-texture-delivery-reopen-v1/RESULT.v1.json'
NORMALS = W / 'villa-native-scene-normal-bindings-v1/PLAN.v1.json'
PROBE = W / 'source-graph-imported-normal-decoder-gpu-probe-v1'
ROOT = None

def read(p):
    return json.loads(Path(p).read_text('utf-8-sig'))

def sha(p):
    with Path(p).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def digest(data):
    return hashlib.sha256(data).hexdigest()

def require(value, reason):
    if not value:
        raise RuntimeError(reason)

def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()

def save(name, value):
    p = ROOT / name
    temporary = p.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(value, indent=2), 'utf-8')
    os.replace(temporary, p)

def split_glb(raw):
    require(len(raw) >= 28 and raw[:4] == b'glTF', 'GLB header missing')
    version, total, length = struct.unpack_from('<III', raw, 4)
    require(version == 2 and total == len(raw) and raw[16:20] == b'JSON'
            and length <= 16 * 1024 * 1024, 'Invalid GLB JSON header')
    pos = 20 + length
    require(pos + 8 <= len(raw) and raw[pos + 4:pos + 8] == b'BIN\x00', 'GLB BIN missing')
    count = struct.unpack_from('<I', raw, pos)[0]
    require(pos + 8 + count == len(raw), 'Additional or truncated GLB chunks')
    doc = json.loads(raw[20:pos])
    require(len(doc['buffers']) == 1 and 'uri' not in doc['buffers'][0]
            and 0 <= count - doc['buffers'][0]['byteLength'] <= 3, 'Single embedded buffer required')
    return doc, raw[pos + 8:]

def pack_glb(doc, data):
    encoded = json.dumps(doc, separators=(',', ':'), ensure_ascii=False, allow_nan=False).encode('utf-8')
    encoded += b' ' * (-len(encoded) % 4)
    data += b'\x00' * (-len(data) % 4)
    return (b'glTF' + struct.pack('<III', 2, 28 + len(encoded) + len(data), len(encoded))
            + b'JSON' + encoded + struct.pack('<I', len(data)) + b'BIN\x00' + data)

def reopen(source_doc, source_bin, path, row, texture_map, edits, sampler_edits):
    doc, data = split_glb(path.read_bytes())
    require(data[:len(source_bin)] == source_bin, 'Original geometry/shader/PNG/meta BIN bytes changed')
    restored = copy.deepcopy(doc)
    for material in restored.get('materials', []):
        contract = material.get('extensions', {}).get('OPENRPG_source_graph')
        if contract is not None:
            contract['schema_version'] = 2
            for texture in contract['textures']:
                texture.pop('imported_storage')
    for edit in edits:
        restored['materials'][edit['material_index']]['extensions']['OPENRPG_source_graph']['normal_decode_modes'][edit['slot']] = edit['before']
    for edit in sampler_edits:
        restored['materials'][edit['material_index']]['extensions']['OPENRPG_source_graph']['textures'][edit['slot']]['mip_bias'] = edit['before']
    restored['bufferViews'] = restored['bufferViews'][:len(source_doc['bufferViews'])]
    restored['buffers'] = copy.deepcopy(source_doc['buffers'])
    require(restored == source_doc, 'JSON changed outside storage, admitted normal modes and measured sampler resolution')
    bindings, mips = 0, 0
    for material in doc.get('materials', []):
        contract = material.get('extensions', {}).get('OPENRPG_source_graph')
        if contract is None:
            continue
        require(contract['schema_version'] == 3, 'Captured texture contract must be version 3')
        for texture in contract['textures']:
            native = texture_map[texture['source_meta_sha256']]
            descriptor = texture['imported_storage']
            expected = copy.deepcopy(native['descriptor_template'])
            for field in ('storage', 'capture'):
                view_index = descriptor[field]['buffer_view']
                view = doc['bufferViews'][view_index]
                require(view['buffer'] == 0 and view_index >= len(source_doc['bufferViews']), 'New embedded view required')
                offset, size = view.get('byteOffset', 0), view['byteLength']
                payload = data[offset:offset + size]
                require(len(payload) == size and digest(payload) == expected[field]['sha256'], 'Embedded payload hash mismatch')
                expected[field]['buffer_view'] = view_index
                if field == 'storage':
                    for mip in expected['mips']:
                        require(digest(payload[mip['offset']:mip['offset'] + mip['bytes']]) == mip['sha256'], 'Embedded mip hash mismatch')
                    mips += len(expected['mips'])
            require(descriptor == expected, 'Descriptor differs from native evidence')
            bindings += 1
    return {'asset_id': row['asset_id'], 'glb': str(path), 'sha256': sha(path),
            'source_glb': row['glb'], 'source_glb_sha256': row['expected_original_glb_sha256'],
            'unchanged_original_bin_sha256': digest(source_bin),
            'texture_bindings_reopened': bindings, 'mip_associations_reopened': mips,
            'normal_binding_edits': edits, 'measured_sampler_resolution': sampler_edits,
            'geometry_and_programs_preserved': True,
            'canonical_reader_accepted': False, 'source_frame_math_qualified': False,
            'gpu_rendered': False, 'studio_verified': False, 'full_import': False}

def convert(row, texture_map, normals):
    raw = Path(row['glb']).read_bytes()
    require(digest(raw) == row['expected_original_glb_sha256'], 'Native source GLB changed')
    source_doc, source_bin = split_glb(raw)
    require(source_doc.get('extras', {}).get('unity_captured_vertices'), 'Native geometry evidence required')
    if any(m['normal_slots'] for m in row['materials']):
        doc, edits = change_document(source_doc, row, normals)
    else:
        doc, edits = copy.deepcopy(source_doc), []
    data = bytearray(source_bin)
    appended = {}
    sampler_edits = []
    def embed(path, expected):
        if expected in appended:
            return appended[expected]
        payload = Path(path).read_bytes()
        require(digest(payload) == expected, 'Captured payload changed before embedding')
        data.extend(b'\x00' * (-len(data) % 4))
        index = len(doc['bufferViews'])
        doc['bufferViews'].append({'buffer': 0, 'byteOffset': len(data), 'byteLength': len(payload)})
        data.extend(payload)
        appended[expected] = index
        return index
    for material_index, material in enumerate(doc.get('materials', [])):
        contract = material.get('extensions', {}).get('OPENRPG_source_graph')
        if contract is None:
            continue
        require(contract['schema_version'] == 2, 'Expected unchanged historical texture contract')
        for slot, texture in enumerate(contract['textures']):
            require('imported_storage' not in texture, 'Unexpected existing capture descriptor')
            native = texture_map[texture['source_meta_sha256']]
            require(native['original_png_sha256'] == texture['png']['sha256'], 'Texture source identity changed')
            descriptor = copy.deepcopy(native['descriptor_template'])
            sampler = descriptor['sampler']
            require(texture['is_srgb'] == descriptor['format'].endswith('_srgb') and
                    texture['source_mipmaps'] == sampler['source_mipmaps_enabled'], 'Authored color space or mipmaps disagree with capture')
            require(texture['filter_mode'] < 0 or texture['filter_mode'] == sampler['filter_mode'], 'Authored filter disagrees with capture')
            require(all(a < 0 or a == b for a, b in zip(texture['wrap'], sampler['wrap'])), 'Authored wrap disagrees with capture')
            require(texture['anisotropy'] < 0 or (texture['anisotropy'] == 0 and sampler['anisotropy'] == 1)
                    or texture['anisotropy'] == sampler['anisotropy'], 'Authored anisotropy disagrees with capture')
            if struct.pack('<f', texture['mip_bias']) != struct.pack('<f', sampler['mip_bias']):
                # Transport v3 uses the measured imported sampler. Original importer
                # bytes and the source derivative remain intact; this is not a
                # claim about defaults or equivalence to the original Unity version.
                captured = read(native['capture_path'])['cases'][descriptor['capture_case_index']]
                imported_bias = json.loads(captured['imported_settings_json'])['m_TextureSettings']['m_MipBias']
                require(texture['mip_bias'] == -1.0 and imported_bias == captured['mip_bias'] == sampler['mip_bias'] == 0.0,
                        'Unqualified sampler discrepancy; original importer and native sampler must be reconciled')
                sampler_edits.append({'material_index': material_index, 'slot': slot,
                    'before': texture['mip_bias'], 'after': sampler['mip_bias'],
                    'original_meta_sha256': texture['source_meta_sha256'],
                    'native_capture_sha256': descriptor['capture']['sha256'],
                    'native_capture_case_index': descriptor['capture_case_index'],
                    'importer_before_readability_override_and_texture_sampler_agree': True,
                    'original_unity_version_equivalence_qualified': False})
                texture['mip_bias'] = sampler['mip_bias']
            descriptor['storage']['buffer_view'] = embed(native['storage_path'], descriptor['storage']['sha256'])
            descriptor['capture']['buffer_view'] = embed(native['capture_path'], descriptor['capture']['sha256'])
            texture['imported_storage'] = descriptor
        contract['schema_version'] = 3
    doc['buffers'][0]['byteLength'] = len(data)
    folder = ROOT / 'jobs' / row['asset_id']
    folder.mkdir(parents=True, exist_ok=False)
    temporary = folder / 'model.glb.unverified'
    temporary.write_bytes(pack_glb(doc, bytes(data)))
    result = reopen(source_doc, source_bin, temporary, row, texture_map, edits, sampler_edits)
    final = folder / 'model.glb'
    os.replace(temporary, final)
    result['glb'] = str(final)
    return result

def run(batch_id):
    state = {'batch_id': batch_id, 'status': 'reopening_evidence', 'started_utc': now(),
             'rows': [], 'full_import': False, 'studio_runtime_qualified': 0}
    save('RUN-RESULT.v1.json', state)
    try:
        prepared = read(ROOT / 'PREPARED.v1.json')
        for p, expected in prepared['input_pins'].items():
            require(sha(p) == expected, 'Prepared input changed: ' + p)
        batches, textures, plan = read(BATCHES), read(TEXTURES), read(NORMALS)
        require(sha(TEXTURES) == batches['texture_receipt_sha256'], 'Closed physical texture receipt changed')
        require(textures['verified_textures'] == 44 and textures['verified_mips'] == 528, 'Closed texture coverage required')
        pins = dict(textures['input_pins'])
        pins.update(plan['input_pins'])
        parity = read(PROBE / 'DECODER-PARITY.v1.json')
        require(parity['candidate_mode2_matches_float_rgorag_gpu_reference'] and parity['all_match_builtin_unpack_reference'], 'Normal decoding comparison required')
        pins.update(parity['proof_pins'])
        for p, expected in pins.items():
            require(sha(p) == expected, 'Pinned evidence changed: ' + p)
        passing = {r['asset_id'] for r in parity['rows'] if r['matches_float_rgorag_reference'] and r['matches_builtin_unpack_reference']}
        normals = {}
        for r in read(PROBE / 'INPUT.v1.json')['rows']:
            if r['asset_id'] in passing:
                for field in ('original_png', 'original_meta', 'storage'):
                    require(sha(r[field]) == r[field + '_sha256'], 'Normal source changed')
                normals[r['original_meta_sha256']] = r
        batch = next(b for b in batches['batches'] if b['id'] == batch_id)
        require(len(batch['asset_ids']) == batch['expected_scenes'], 'Batch scope mismatch')
        texture_map = {r['original_meta_sha256']: r for r in textures['rows']}
        source_map = {r['asset_id']: r for r in plan['rows']}
        save('EVIDENCE-REOPEN.v1.json', {'checked_utc': now(), 'files_rehashed': len(pins),
             'normal_source_cases_rehashed': len(normals), 'input_pins': pins, 'full_import': False})
        state.update(status='assembling_native_glb_derivatives', expected_scenes=batch['expected_scenes'])
        save('RUN-RESULT.v1.json', state)
        for asset_id in batch['asset_ids']:
            row = source_map[asset_id]
            require(all(s['proposed_mode'] is not None for m in row['materials'] for s in m['normal_slots']), 'Unresolved normal remains deferred')
            state['rows'].append(convert(row, texture_map, normals))
            save('RUN-RESULT.v1.json', state)
        require(len(state['rows']) == batch['expected_scenes'], 'Incomplete batch')
        save('INPUTS.v1.json', [{k: r[k] for k in ('asset_id', 'glb', 'sha256')} for r in state['rows']])
        state.update(status='native_glb_storage_and_normal_bindings_reopened', verified_derivatives=len(state['rows']))
    except Exception as error:
        state.update(status='refused', error=str(error))
    state['finished_utc'] = now()
    save('RUN-RESULT.v1.json', state)
    print(json.dumps({k: v for k, v in state.items() if k != 'rows'}))
    if state['status'] == 'refused':
        raise SystemExit(1)

def prepare(batch_id):
    require(not ROOT.exists(), 'Existing batch evidence preserved')
    ROOT.mkdir()
    helper = Path(__file__).resolve()
    inputs = [BATCHES, TEXTURES, NORMALS, helper, helper.with_name('apply_scene_normal_bindings.py'), PROBE / 'DECODER-PARITY.v1.json']
    save('PREPARED.v1.json', {'batch_id': batch_id, 'created_utc': now(), 'input_pins': {str(p): sha(p) for p in inputs}})
    with (ROOT / 'controller.log').open('xb') as log:
        process = subprocess.Popen([sys.executable, '-B', str(helper), 'run', batch_id, ROOT.name.rsplit('-v', 1)[1]], cwd=ROOT,
            stdout=log, stderr=log, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', {'pid': process.pid, 'launch_utc': now(), 'batch_id': batch_id})
    print(json.dumps({'controller_pid': process.pid, 'root': str(ROOT), 'batch_id': batch_id}))

if __name__ == '__main__':
    action, batch_id = sys.argv[1:3]
    revision = sys.argv[3] if len(sys.argv) == 4 else '1'
    require(revision.isdigit() and int(revision) > 0, 'Positive evidence revision required')
    require(batch_id in {f'L04.VILLA.{n:02d}' for n in range(1, 12)}, 'Known documented batch required')
    ROOT = W / ('villa-native-scene-delivery-' + batch_id.lower().replace('.', '-') + '-v' + revision)
    {'prepare': prepare, 'run': run}[action](batch_id)
