"""Identify whether LOD counters describe different native imported meshes."""
from pathlib import Path
import datetime
import hashlib
import json

W = Path('F:/OpenRPG_ImportWork')
N = W / 'rocks-native-mesh-counter-v1'
ROOT = W / 'rocks-native-mesh-counter-reopen-v1'


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def differences(left, right, path=''):
    if isinstance(left, dict) and isinstance(right, dict):
        rows = []
        for key in sorted(set(left) | set(right)):
            if key not in left or key not in right:
                rows.append(dict(path=path + '/' + key, previous=left.get(key), current=right.get(key)))
            else:
                rows.extend(differences(left[key], right[key], path + '/' + key))
        return rows
    if left != right:
        return [dict(path=path, previous=left, current=right)]
    return []


def main():
    assert not ROOT.exists(), 'Immutable evidence already exists'
    run = read(N / 'RUN-RESULT.v1.json')
    assert run['status'] == 'native_mesh_counters_captured_reopen_pending' and run['actual_unity_exit_code'] == 0
    assert sha(N / 'NATIVE-RESULT.v1.json') == run['native_result_sha256']
    pins = dict(read(N / 'PREPARED.v1.json')['input_pins'])
    for path, digest in pins.items():
        assert sha(path) == digest, path
    native = read(N / 'NATIVE-RESULT.v1.json')
    assert not native['import_settings_changed'] and not native['geometry_streams_captured']
    scene = read(W / 'rocks-firepit-scene-bindings-v1/RESULT.v1.json')['rows'][0]
    bindings = {b['node_path']: b for b in scene['static_renderer_bindings']}
    assert len(native['rows']) == len(bindings) == 57
    seen, current, mesh_differences, importer_differences = set(), {}, [], {}
    for row in native['rows']:
        node = row['node_path']
        assert node in bindings and node not in seen
        seen.add(node)
        binding = bindings[node]
        renderer = binding['renderer_identity']
        mesh = binding['source_mesh_identity']
        assert (row['renderer_guid'], row['renderer_file_id']) == (renderer['guid'], renderer['file_id'])
        assert (row['mesh_guid'], row['mesh_file_id']) == (mesh['guid'], mesh['file_id'])
        assert row['mesh_name'] == mesh['name']
        # Unity reports Assets/HIVEMIND while the original inventory spells
        # Assets/Hivemind. Admit only an actual identical file on this Windows
        # volume, rather than treating arbitrary path changes as equivalent.
        project = W / 'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
        assert row['mesh_path'].casefold() == mesh['asset_path'].casefold()
        assert (project / row['mesh_path']).samefile(project / mesh['asset_path'])
        source = W / 'hivemind-run/native-american-village/source' / mesh['asset_path']
        private = project / row['mesh_path']
        for original, copied in [(source, private), (Path(str(source) + '.meta'), Path(str(private) + '.meta'))]:
            digest = sha(original)
            assert sha(copied) == digest, ('source_copy_changed', original, copied)
            pins[str(original)] = digest
            pins[str(copied)] = digest
        assert all(t == 'Triangles' for t in row['topologies'])
        assert len(row['index_counts']) == row['submeshes'] and all(n % 3 == 0 for n in row['index_counts'])
        path = Path(binding['geometry_derivative']) / 'GEOMETRY.v1.json'
        assert sha(path) == binding['geometry_receipt_sha256']
        pins[str(path)] = binding['geometry_receipt_sha256']
        previous = read(path)
        counters = dict(vertices=row['vertices'], triangles=sum(row['index_counts']) // 3, submeshes=row['submeshes'])
        current[(renderer['guid'], renderer['file_id'])] = counters
        changes = differences({k: previous['geometry'][k] for k in counters}, counters)
        if changes:
            mesh_differences.append(dict(node_path=node, mesh_guid=mesh['guid'], mesh_file_id=mesh['file_id'],
                                         mesh_name=mesh['name'], differences=changes))
        old_importer = json.loads(previous['metadata']['imported_settings_json'])
        new_importer = json.loads(row['importer_json'])
        changes = differences(old_importer, new_importer)
        if changes:
            importer_differences[row['mesh_path']] = changes
    metric = read(W / 'rocks-firepit-native-lod-metric-v1/NATIVE-RESULT.v1.json')
    groups = {g['node_path']: g for g in scene['original_lod_groups']}
    metrics_checked = 0
    api_discrepancies = []
    for sample in metric['samples']:
        if not sample['prefab_path'].endswith('/SM_rocks_002.prefab'):
            continue
        fields = {f['name']: f for f in sample['native_fields']}
        chosen = fields['activeLODLevel']['integer_value']
        renderers = [] if chosen == -1 else groups[sample['node_path']]['levels'][chosen]['renderer_components']
        counters = dict(triangleCount=0, vertexCount=0, submeshCount=0, rendererCount=len(renderers))
        for renderer in renderers:
            mesh = current[(renderer['guid'], renderer['file_id'])]
            for key, source in [('triangleCount', 'triangles'), ('vertexCount', 'vertices'), ('submeshCount', 'submeshes')]:
                counters[key] += mesh[source]
        for key, value in counters.items():
            actual = fields[key]['integer_value']
            if actual != value:
                api_discrepancies.append(dict(node_path=sample['node_path'], fov=sample['field_of_view'],
                                             height=sample['requested_relative_height'], counter=key,
                                             native_lod=actual, native_mesh=value))
        metrics_checked += 1
    assert metrics_checked == 273
    for path, digest in pins.items():
        assert sha(path) == digest, path
    for path in [Path(__file__), N / 'NATIVE-RESULT.v1.json', N / 'PREPARED.v1.json', N / 'RUN-RESULT.v1.json',
                 W / 'rocks-firepit-native-lod-metric-v1/NATIVE-RESULT.v1.json']:
        pins[str(path)] = sha(path)
    result = dict(status='native_mesh_identity_and_counter_comparison_complete', renderers=57,
                  rocks_lod_samples_compared=metrics_checked, mesh_counter_differences=mesh_differences,
                  native_lod_vs_native_mesh_discrepancies=api_discrepancies,
                  importer_setting_differences=importer_differences,
                  native_stream_equivalence_qualified=False, full_import=False,
                  input_pins=pins, source_pins_unchanged=True,
                  finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    ROOT.mkdir()
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps(dict(status=result['status'], mesh_counter_differences=len(mesh_differences),
                         api_discrepancies=len(api_discrepancies), importer_paths=len(importer_differences))))


if __name__ == '__main__':
    main()
