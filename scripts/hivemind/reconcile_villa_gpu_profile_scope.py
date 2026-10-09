"""Keep old measured fetch-default profiles separate from current native scenes."""
import datetime
import hashlib
import json
from pathlib import Path
import struct

WORK = Path('F:/OpenRPG_ImportWork')
ROOT = WORK / 'villa-gpu-profile-current-native-scope-v1'
PLAN = WORK / 'villa-native-scene-delivery-plan-v1/PLAN.v1.json'
DEFERRED = WORK / 'villa-all-deferred-source-identity-reopen-v1/RESULT.v1.json'
PROFILES = WORK / 'villa-unity-source-gpu-input-profiles-v1/RESULT.v1.json'
OLD_LINK = WORK / 'villa-gpu-input-derivative-link-v1/RESULT.v1.json'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def header(path):
    with Path(path).open('rb') as stream:
        prefix = stream.read(20)
        assert len(prefix) == 20 and prefix[:4] == b'glTF' and prefix[16:20] == b'JSON'
        length = struct.unpack_from('<I', prefix, 12)[0]
        assert 0 < length <= 16 * 1024 * 1024
        data = stream.read(length)
        assert len(data) == length
    return json.loads(data), hashlib.sha256(data).hexdigest()


def main():
    assert not ROOT.exists(), 'Existing scope evidence preserved'
    pins = {str(path): sha(path) for path in [PLAN, DEFERRED, PROFILES, OLD_LINK, Path(__file__).resolve()]}
    plan, deferred, profiles, old = read(PLAN), read(DEFERRED), read(PROFILES), read(OLD_LINK)
    current = {row['asset_id']: row for row in plan['rows']}
    assert len(current) == len(plan['rows']) == 226
    assert len(deferred['rows']) == 19 and len(profiles['rows']) == 14
    current_meshes = {(row['unity_mesh_guid'], str(row['unity_mesh_file_id'])) for row in current.values()}
    profile_meshes = {(row['unity_mesh_guid'], str(row['unity_mesh_file_id'])) for row in profiles['rows']}
    intersections = sorted(current_meshes.intersection(profile_meshes))
    old_ids = {row['asset_id'] for row in old['rows']}
    rows = []
    for row in deferred['rows']:
        declared = current[row['asset_id']]
        assert Path(row['glb']) == Path(declared['glb'])
        assert row['full_glb_sha256'] == declared['expected_glb_sha256']
        document, header_sha = header(row['glb'])
        assert header_sha == declared['header_json_sha256']
        buffers = document['extras']['unity_captured_vertices']['capture_buffers']
        rows.append({'asset_id': row['asset_id'], 'glb': row['glb'],
            'json_header_sha256': header_sha,
            'native_mesh_guid': declared['unity_mesh_guid'],
            'native_mesh_file_id': str(declared['unity_mesh_file_id']),
            'captured_tangent_bytes_declared': buffers['tangents.f32']['bytes'],
            'matching_older_gpu_profile': (declared['unity_mesh_guid'],
                str(declared['unity_mesh_file_id'])) in profile_meshes,
            'full_glb_sha_from_prior_reopen': row['full_glb_sha256']})
    for path, expected in pins.items():
        assert sha(path) == expected, 'Scope input changed'
    for row in rows:
        assert header(row['glb'])[1] == row['json_header_sha256'], 'Current header changed'
    assert not intersections, 'Unexpected overlap needs individual profile reconciliation'
    assert all(row['captured_tangent_bytes_declared'] > 0 for row in rows)
    result = {'schema': 'openrpg.villa-measured-gpu-profile-scope.v1',
        'status': 'older_profiles_not_applicable_to_current_native_scene_identities',
        'finished_utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
        'current_native_logical_scenes': 226, 'current_deferred_scenes': 19,
        'older_measured_gpu_profiles': 14, 'older_linked_derivatives': len(old['rows']),
        'exact_mesh_identity_intersections': intersections,
        'old_linked_asset_id_intersections': sorted(old_ids.intersection(current)),
        'current_deferred_declared_tangents_present': 19,
        'rows': rows, 'input_document_pins_reopened_before_after': pins,
        'json_headers_reopened_before_after': True,
        'full_glb_payloads_rehashed_this_scope_review': False,
        'measured_defaults_transplanted': False,
        'current_deferred_material_binding_resolved': False,
        'older_profile_evidence_preserved': True, 'gpu_render_qualified': False,
        'studio_verified': False, 'full_import': False}
    ROOT.mkdir()
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2) + '\n', 'utf-8')
    print(json.dumps({key: value for key, value in result.items()
                     if key not in ['rows', 'input_document_pins_reopened_before_after']}))


if __name__ == '__main__':
    main()
