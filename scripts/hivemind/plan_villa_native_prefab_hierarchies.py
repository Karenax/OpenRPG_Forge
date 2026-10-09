"""Plan native prefab pose/binding capture for all 226 Villa logical scenes.

Read only metadata and the hash-bound original snapshot; this is not a renewed
full GLB validation, a Unity capture, or permission to execute purchased code.
"""
import hashlib
import json
from pathlib import Path
import re
import struct

WORK = Path('F:/OpenRPG_ImportWork')
ROOT = WORK/'villa-native-prefab-hierarchy-plan-v1'
SOURCE = WORK/'hivemind-run/villa-forge/source'
INPUT = WORK/'villa-unity-captured-vertex-glb-batch-v6/RESULT.v1.json'
HELPER = WORK/'hivemind-prefab-instance-source-closure-v1/PrefabHierarchySnapshot.cs'
API = HELPER.parent/'API-COMPILE.v3.json'


def read(path):
    return json.loads(path.read_text('utf-8-sig'))


def sha(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def safe_source(relative):
    rel = Path(relative)
    assert not rel.is_absolute() and '..' not in rel.parts and rel.parts[0] == 'Assets'
    path = SOURCE/rel
    assert path.is_file()
    return path


def snapshot(path):
    with path.open('rb') as stream:
        header = stream.read(20)
        assert header[:4] == b'glTF' and header[16:20] == b'JSON'
        length = struct.unpack_from('<I', header, 12)[0]
        doc = json.loads(stream.read(length))
        extra = doc['extras']
        view = doc['bufferViews'][extra['source_original_snapshot_view']]
        assert view['buffer'] == 0
        stream.seek(28 + length + view.get('byteOffset', 0))
        raw = stream.read(view['byteLength'])
    assert hashlib.sha256(raw).hexdigest() == extra['source_original_snapshot_sha256']
    return json.loads(raw)['job'], extra['source_original_snapshot_sha256']


def main():
    assert not ROOT.exists(), 'Existing plan retained'
    native = read(INPUT)
    assert native['total'] == len(native['rows']) == 226
    assert len({r['asset_id'] for r in native['rows']}) == 226
    api = read(API)
    assert api['actual_compiler_exit_code'] == 0 and api['helper_compiled_against_installed_unity_api']
    assert sha(HELPER) == api['helper_sha256']
    roots, files, aliases = {}, {}, []
    for row in native['rows']:
        job, snapshot_sha = snapshot(Path(row['glb']))
        source_prefab = safe_source(job['source'])
        assert source_prefab.suffix.lower() == '.prefab'
        assert sha(source_prefab) == job['source_sha256']
        meta = Path(str(source_prefab)+'.meta')
        match = re.search(r'^guid:\s*([0-9a-f]{32})\s*$', meta.read_text('utf-8-sig'), re.M)
        assert match and match.group(1) == job['guid']
        key = job['guid']
        if key not in roots:
            roots[key] = {'asset_id': row['asset_id'], 'asset_path': job['source'],
                          'expected_guid': key, 'source_sha256': job['source_sha256'],
                          'source_meta_sha256': sha(meta), 'logical_asset_ids': []}
        else:
            assert roots[key]['asset_path'] == job['source']
            assert roots[key]['source_sha256'] == job['source_sha256']
        roots[key]['logical_asset_ids'].append(row['asset_id'])
        files[str(source_prefab)] = job['source_sha256']
        files[str(meta)] = roots[key]['source_meta_sha256']
        expected = []
        for component in job['components']:
            mesh = safe_source(component['mesh_source'])
            assert mesh.suffix.lower() == '.fbx'
            if str(mesh) in files:
                assert files[str(mesh)] == component['mesh_sha256']
            files[str(mesh)] = component['mesh_sha256']
            mesh_meta = Path(str(mesh)+'.meta')
            files[str(mesh_meta)] = sha(mesh_meta)
            expected.append({'source_transform_id': str(component['transform_id']),
                             'source_gameobject_id': str(component['gameobject_id']),
                             'mesh_guid': component['mesh_guid'],
                             'mesh_file_id': str(component['mesh_fileid']),
                             'material_guids': component['material_guids'],
                             'source_lod': component.get('lod')})
        assert expected
        aliases.append({'asset_id': row['asset_id'], 'prefab_guid': key,
                        'original_snapshot_sha256': snapshot_sha,
                        'declared_glb': row['glb'], 'declared_glb_sha256': row['glb_sha256'],
                        'expected_source_renderer_bindings': expected})
    for path, expected in files.items():
        assert sha(Path(path)) == expected, 'Original source/metadata changed'
    pins = {str(p): sha(p) for p in [Path(__file__).resolve(), INPUT, HELPER, API]}
    ROOT.mkdir()
    result = {'status': '226_logical_scene_native_prefab_capture_scope_prepared',
              'logical_scenes': 226, 'unique_prefab_roots': len(roots),
              'source_geometry_and_metadata_files_reopened': len(files),
              'requests': list(roots.values()), 'logical_scene_bindings': aliases,
              'source_pins': files, 'input_pins': pins,
              'trusted_helper': str(HELPER), 'trusted_helper_sha256': api['helper_sha256'],
              'native_captures_executed': False, 'isolated_project_created': False,
              'purchased_scripts_copied_or_executed': False,
              'materials_textures_and_shader_dependencies_still_require_separate_preparation': True,
              'full_glb_bytes_revalidated_in_this_plan': False,
              'gpu_rendered': False, 'studio_verified': False, 'full_import': False}
    (ROOT/'PLAN.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps({k: v for k, v in result.items() if not isinstance(v, (list, dict))}))


if __name__ == '__main__':
    main()
