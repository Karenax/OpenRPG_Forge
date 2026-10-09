"""Compare actual editor stream bytes to the original captured stream pins."""
from pathlib import Path
import datetime
import hashlib
import json

W = Path('F:/OpenRPG_ImportWork')
N = W / 'rocks-native-mesh-streams-v1'
ROOT = W / 'rocks-native-mesh-streams-reopen-v1'


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    assert not ROOT.exists(), 'Immutable evidence already exists'
    run = read(N / 'RUN-RESULT.v1.json')
    assert run['status'] == 'native_mesh_streams_captured_reopen_pending' and run['actual_unity_exit_code'] == 0
    assert sha(N / 'NATIVE-RESULT.v1.json') == run['native_result_sha256']
    pins = dict(read(N / 'PREPARED.v1.json')['input_pins'])
    for path, digest in pins.items():
        assert sha(path) == digest, path
    scene = read(W / 'rocks-firepit-scene-bindings-v1/RESULT.v1.json')['rows'][0]
    expected = {}
    for binding in scene['static_renderer_bindings']:
        identity = binding['source_mesh_identity']
        key = (identity['guid'], identity['file_id'])
        path = Path(binding['geometry_derivative']) / 'GEOMETRY.v1.json'
        assert sha(path) == binding['geometry_receipt_sha256']
        pins[str(path)] = binding['geometry_receipt_sha256']
        if key in expected:
            assert expected[key]['path'] == path
        expected[key] = dict(path=path, receipt=read(path))
    native = read(N / 'NATIVE-RESULT.v1.json')
    assert not native['import_settings_changed'] and not native['prefab_instantiated']
    seen, discrepancies, checked, byte_count = set(), [], 0, 0
    stream_pins = {}
    for row in native['meshes']:
        key = (row['mesh_guid'], row['mesh_file_id'])
        assert key in expected and key not in seen
        seen.add(key)
        previous = expected[key]['receipt']
        assert row['vertices'] == previous['geometry']['vertices'] and row['submeshes'] == previous['geometry']['submeshes']
        original_streams = previous['source_stream_pins']
        by_name = {Path(path).name: (path, digest) for path, digest in original_streams.items()}
        assert len(by_name) == len(original_streams) and set(row['streams']) == set(by_name)
        assert len(row['streams']) == len(set(row['streams']))
        directory = Path(row['directory'])
        assert directory.resolve().parent == N.resolve() and directory.name == '-'.join(key)
        for name in row['streams']:
            original, digest = by_name[name]
            assert sha(original) == digest
            pins[original] = digest
            path = directory / name
            current = sha(path)
            stream_pins[str(path)] = current
            checked += 1
            byte_count += path.stat().st_size
            if current != digest:
                discrepancies.append(dict(mesh_guid=key[0], mesh_file_id=key[1], stream=name,
                                          prior_sha256=digest, native_sha256=current,
                                          prior_bytes=Path(original).stat().st_size, native_bytes=path.stat().st_size))
    assert seen == set(expected)
    for path, digest in pins.items():
        assert sha(path) == digest, path
    for path, digest in stream_pins.items():
        assert sha(path) == digest, path
    for path in [Path(__file__), N / 'NATIVE-RESULT.v1.json', N / 'PREPARED.v1.json', N / 'RUN-RESULT.v1.json']:
        pins[str(path)] = sha(path)
    result = dict(status=('refused_native_stream_differences' if discrepancies else 'native_editor_mesh_streams_exactly_match_previous_capture'),
                  meshes=len(seen), stream_files=checked, captured_bytes=byte_count,
                  byte_equivalence_verified=not discrepancies, discrepancies=discrepancies,
                  original_pins_unchanged=True, actual_api=native['actual_editor_api'],
                  import_settings_changed=False, full_import=False, gpu_runtime_qualified=False,
                  input_pins=pins, captured_stream_pins=stream_pins,
                  finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    ROOT.mkdir()
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps({k: result[k] for k in ['status', 'meshes', 'stream_files', 'captured_bytes', 'byte_equivalence_verified']}))


if __name__ == '__main__':
    main()
