"""Variable-size subset adapter; reuse the original geometry conversion and reopener."""
import datetime
import importlib.util
import json
from pathlib import Path
import sys

import convert_verified_townsmith_batch as original
from assemble_native_villa_delivery import read, require, sha
from convert_closed_nested_geometry import reopen_streams


def main(start, count):
    require(start >= 80 and 0 < count <= 20 and start + count <= 893, 'Bounded remaining scope required')
    suffix = f'{start:04d}-{start + count - 1:04d}-v1'
    receipt_path = original.W / ('townsmith-native-partial-reopen-' + suffix) / 'RESULT.v1.json'
    root = original.W / ('townsmith-native-geometry-' + suffix)
    require(not root.exists(), 'Existing geometry evidence must be reviewed, not overwritten')
    receipt = read(receipt_path)
    require(receipt['status'] == 'closed_native_townsmith_cases_independently_reopened'
            and receipt['verified_native_mesh_cases'] == count
            and receipt['original_selected_sources_unchanged'], 'Closed native subset required')
    require([r['native_case_ordinal'] for r in receipt['rows']] == list(range(start, start + count)),
            'Exact ordered subset required')
    spec = importlib.util.spec_from_file_location('unchanged_native_geometry_converter', original.CONVERTER)
    converter = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(converter)
    root.mkdir()
    state = {'schema': 'openrpg.native-static-geometry-subset-conversion.v1',
             'status': 'converting_verified_cases', 'expected_meshes': count, 'start_ordinal': start,
             'pack_id': 'hivemind.townsmith', 'rows': [], 'glb_created': False,
             'shader_source_frames_qualified': False, 'material_render_qualified': False,
             'studio_verified': False, 'full_import': False, 'whole_native_capture_complete': False,
             'started_utc': datetime.datetime.now(datetime.timezone.utc).isoformat()}
    def save():
        p = root / 'RESULT.v1.json'
        tmp = p.with_suffix('.json.tmp')
        tmp.write_text(json.dumps(state, indent=2), 'utf-8')
        tmp.replace(p)
    try:
        pins = dict(receipt['input_pins'])
        for p in [receipt_path, original.CONVERTER, original.CONTROLS, Path(original.__file__),
                  Path(__file__).resolve(), Path(__file__).with_name('convert_closed_nested_geometry.py')]:
            pins[str(p)] = sha(p)
        for p, expected in pins.items():
            require(sha(p) == expected, 'Verified source changed: ' + p)
        snapshot = read(receipt['rows'][0]['capture_snapshot'])
        manifest = read(original.SOURCE / 'manifest.json')
        for case in receipt['rows']:
            ordinal = case['native_case_ordinal']
            metadata, request = snapshot['cases'][ordinal], manifest['cases'][ordinal]
            require(metadata['asset_id'] == request['asset_id'] == case['asset_id'], 'Native identity mismatch')
            source = original.SOURCE / 'export' / f'{ordinal:03d}'
            raw = {name: (source / name).read_bytes() for name in case['buffers']}
            target = root / 'jobs' / f'{ordinal:03d}'
            identity = {'guid': request['expected_guid'], 'file_id': request['expected_file_id']}
            converted = converter.convert_closed_case(source, metadata, identity, target)
            outputs = {name: (target / name).read_bytes() for name in converted['derived_streams']}
            checked = reopen_streams(raw, outputs, metadata)
            for name, details in converted['derived_streams'].items():
                require(sha(target / name) == details['sha256'], 'Derived stream hash mismatch')
            state['rows'].append({'asset_id': case['asset_id'], 'capture_ordinal': ordinal,
                'source_mesh_identity': identity, 'derivative': str(target),
                'geometry_receipt_sha256': sha(target / 'GEOMETRY.v1.json'),
                'derived_streams_reopened': checked, 'vertices': metadata['vertices'],
                'submeshes': metadata['submeshes'], 'prefab_instance_aliases': case['prefab_instance_aliases'],
                'original_raw_buffers_preserved': True,
                'source_frame_note': 'Final source shader attributes require original captured Unity vectors; reflected intermediate streams do not qualify that frame.'})
            save()
        for p, expected in pins.items():
            require(sha(p) == expected, 'Source changed during conversion: ' + p)
        state.update(status='verified_townsmith_geometry_derivatives_reopened', verified_meshes=count,
                     input_pins=pins, unchanged_converter_reused=True,
                     derived_streams_reopened=sum(r['derived_streams_reopened'] for r in state['rows']))
    except Exception as error:
        state.update(status='refused_or_partial', error=str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save()
    require(state['status'] != 'refused_or_partial', state.get('error', 'Subset refused'))
    print(json.dumps({k: v for k, v in state.items() if k not in ('rows', 'input_pins')}))


if __name__ == '__main__':
    main(int(sys.argv[1]), int(sys.argv[2]))
