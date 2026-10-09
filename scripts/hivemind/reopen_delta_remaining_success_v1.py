"""Reopen one genuine successful case from the failed two-case export; retain both scopes."""
from pathlib import Path
import copy
import json
import math
import struct
import hashlib

from verify_native_urp_material_snapshot import reconcile, sha

W = Path('F:/OpenRPG_ImportWork')
PRIOR = W / 'unity-delta-remaining-native-materials-v2'
ROOT = W / 'delta-rockstemperate-native-independent-reopen-v1'
GUID = 'abc00000000014678987794877247210'


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def save(name, value):
    with (ROOT / name).open('x', encoding='utf-8') as stream:
        json.dump(value, stream, indent=2, allow_nan=False)


def f32(value):
    assert type(value) in (int, float) and math.isfinite(value)
    return struct.unpack('<f', struct.pack('<f', value))[0]


def main():
    assert not ROOT.exists()
    run, prepared, manifest, native = [read(PRIOR / n) for n in
        ['RUN-RESULT.v1.json','PREPARED.v1.json','manifest.v1.json','NATIVE-RESULT.v1.json']]
    assert run['actual_unity_exit_code'] == 1 and run.get('finished_utc')
    assert native['requested'] == 2 and native['captured'] == native['failed'] == 1
    success = [r for r in native['cases'] if r['material_guid'] == GUID]
    assert len(success) == 1 and not success[0]['error']
    refusal = [r for r in native['cases'] if r['material_guid'] != GUID]
    assert len(refusal) == 1 and 'Exact original shader GUID/fileID required' in refusal[0]['error']
    projected_manifest = copy.deepcopy(manifest)
    projected_manifest['cases'] = [r for r in manifest['cases'] if r['material_guid'] == GUID]
    projected = copy.deepcopy(native)
    projected.update(cases=copy.deepcopy(success), requested=1, captured=1, failed=0)
    assert len(reconcile(projected_manifest, projected)) == 1
    assert sum(len(r['required_textures']) for r in projected_manifest['cases']) == 7
    pins = dict(prepared['input_pins'])
    for p in [Path(__file__).resolve(), PRIOR / 'RUN-RESULT.v1.json', PRIOR / 'PREPARED.v1.json',
              PRIOR / 'NATIVE-RESULT.v1.json', PRIOR / 'manifest.v1.json',
              Path(__file__).with_name('verify_native_urp_material_snapshot.py')]:
        pins[str(p)] = sha(p)
    reconciled_logs = []
    for p,digest in list(pins.items()):
        if sha(p) == digest:
            continue
        historic = W / 'unity-delta-remaining-native-materials-v1/unity.log'
        assert Path(p) == historic, 'Changed source or capture input: ' + p
        raw = historic.read_bytes()
        hasher, offset, prefix = hashlib.sha256(), 0, None
        for line in raw.splitlines(keepends=True):
            hasher.update(line)
            offset += len(line)
            if hasher.hexdigest() == digest:
                prefix = raw[:offset]
                break
        assert prefix is not None and len(prefix) < len(raw), 'Historical log changed beyond append-only shutdown'
        reconciled_logs.append(dict(path=p, expected_before_sha256=digest,
             observed_after_sha256=hashlib.sha256(raw).hexdigest(), prefix=prefix, raw=raw))
        # A historical shutdown log is not a source input. Preserve its exact
        # previously pinned bytes and appended current bytes in immutable files.
        del pins[p]
    assert all(sha(p) == digest for p,digest in pins.items())
    controls = []
    for name in ['wrong_shader','uncompiled_pass','missing_material','wrong_texture']:
        bad = copy.deepcopy(projected)
        if name == 'wrong_shader': bad['cases'][0]['shader_guid'] = '0'*32
        elif name == 'uncompiled_pass': bad['cases'][0]['passes'][0]['compiled'] = False
        elif name == 'missing_material': bad['cases'].pop()
        else:
            prop = projected_manifest['cases'][0]['required_textures'][0]['property']
            next(r for r in bad['cases'][0]['properties'] if r['name'] == prop)['texture_guid'] = '0'*32
        try: reconcile(projected_manifest, bad)
        except ValueError as error: controls.append(dict(test=name, reason=str(error)))
        else: raise AssertionError('Altered successful capture accepted')
    source_path = W / 'nested-prefab-delta-material-source-ir-v1/RESULT.v1.json'
    source = next(r for r in read(source_path)['rows'] if r['material_identity']['guid'] == GUID)
    original = source['source_material_properties']['m_SavedProperties']
    captured = json.loads(success[0]['native_material_json'])['Material']['m_SavedProperties']
    differences = []
    for key,kind in [('m_Floats','float'),('m_Colors','color')]:
        original_table = {k:v for entry in original[key] for k,v in entry.items()}
        captured_table = {r['first']:r['second'] for r in captured[key]}
        for name,value in original_table.items():
            candidate = captured_table.get(name)
            same = candidate is not None and (f32(value)==f32(candidate) if kind=='float' else
                    all(f32(value[k])==f32(candidate[k]) for k in 'rgba'))
            if not same: differences.append(dict(kind=kind, property=name, original=value, native=candidate))
    pins[str(source_path)] = sha(source_path)
    pins[source['source_material']['path']] = source['source_material']['sha256']
    assert all(sha(p) == digest for p,digest in pins.items())
    ROOT.mkdir()
    history = []
    for i,row in enumerate(reconciled_logs):
        prefix_path, raw_path = ROOT / f'historical-log-{i}.pinned-prefix.bin', ROOT / f'historical-log-{i}.appended.bin'
        prefix_path.write_bytes(row['prefix'])
        raw_path.write_bytes(row['raw'])
        assert sha(prefix_path) == row['expected_before_sha256'] and sha(raw_path) == row['observed_after_sha256']
        pins[str(prefix_path)], pins[str(raw_path)] = sha(prefix_path), sha(raw_path)
        history.append(dict(path=row['path'], pinned_prefix=str(prefix_path), appended_snapshot=str(raw_path),
             prefix_sha256=sha(prefix_path), appended_sha256=sha(raw_path),
             delayed_shutdown_append_only_proven=True))
    save('MANIFEST.success-projection.v1.json', projected_manifest)
    save('NATIVE.success-projection.v1.json', projected)
    save('RESULT.v1.json', dict(status='one_original_rockstemperate_material_and_seven_bindings_reopened',
         material_guid=GUID, native_materials_verified=1, active_texture_bindings_verified=7,
         actual_original_export_exit_code=1, original_export_requested=2, original_export_failed=1,
         successful_case_projection_only=True, new_native_capture_executed=False,
         historical_log_append_reconciliation=history,
         original_full_result_sha256=sha(PRIOR / 'NATIVE-RESULT.v1.json'),
         successful_case_index=next(i for i,r in enumerate(native['cases']) if r['material_guid']==GUID),
         source_and_native_saved_float_color_differences=differences,
         original_effective_render_state_comparison_pending=True, refused_mutations=controls,
         failed_firepit_case_preserved=True, input_pins=pins,
         gpu_render_parity_qualified=False, studio_verified=False, full_import=False))
    print(json.dumps(dict(native_materials_reopened=1, active_bindings=7,
         saved_property_differences=len(differences), full_import=False)))


if __name__ == '__main__':
    main()
