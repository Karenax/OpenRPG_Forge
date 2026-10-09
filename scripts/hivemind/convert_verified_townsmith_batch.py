"""Convert only an independently verified native subset; never claim full prefabs."""
import datetime
import importlib.util
import json
from pathlib import Path

from assemble_native_villa_delivery import read, require, sha
from convert_closed_nested_geometry import reopen_streams

W = Path('F:/OpenRPG_ImportWork')
SOURCE = W / 'unity-nested-prefab-static-geometry-capture-v1/townsmith'
RECEIPT = W / 'townsmith-native-partial-reopen-0000-0019-v1/RESULT.v1.json'
ROOT = W / 'townsmith-native-geometry-0000-0019-v1'
CONVERTER = W / 'dungeon-captured-geometry-converter-v1/convert_geometry_batch_v2.py'
CONTROLS = W / 'dungeon-captured-geometry-converter-v1/CONTROLS.v2.json'

def main():
    require(not ROOT.exists(), 'Existing Townsmith derivative evidence preserved')
    receipt = read(RECEIPT)
    require(receipt['status'] == 'closed_native_townsmith_cases_independently_reopened'
            and receipt['verified_native_mesh_cases'] == 20 and receipt['original_selected_sources_unchanged'],
            'Closed selected-case verification required')
    spec = importlib.util.spec_from_file_location('unchanged_geometry_converter', CONVERTER)
    converter = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(converter)
    ROOT.mkdir()
    state = {'schema': 'openrpg.native-static-geometry-subset-conversion.v1',
        'status': 'converting_verified_cases', 'pack_id': 'hivemind.townsmith',
        'started_utc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'expected_meshes': 20,
        'rows': [], 'whole_native_capture_complete': False, 'glb_created': False,
        'shader_source_frames_qualified': False, 'material_render_qualified': False,
        'studio_verified': False, 'full_import': False}
    try:
        pins = dict(receipt['input_pins'])
        for p in [RECEIPT, CONVERTER, CONTROLS, Path(__file__).resolve(), Path(__file__).with_name('convert_closed_nested_geometry.py')]:
            pins[str(p)] = sha(p)
        for p, expected in pins.items():
            require(sha(p) == expected, 'Verified source changed: ' + p)
        snapshot = read(receipt['rows'][0]['capture_snapshot'])
        manifest = read(SOURCE / 'manifest.json')
        for case in receipt['rows']:
            ordinal = case['native_case_ordinal']
            metadata = snapshot['cases'][ordinal]
            request = manifest['cases'][ordinal]
            require(metadata['asset_id'] == request['asset_id'] == case['asset_id'], 'Native case identity differs')
            source = SOURCE / 'export' / f'{ordinal:03d}'
            raw = {name: (source / name).read_bytes() for name in case['buffers']}
            target = ROOT / 'jobs' / f'{ordinal:03d}'
            expected = {'guid': request['expected_guid'], 'file_id': request['expected_file_id']}
            result = converter.convert_closed_case(source, metadata, expected, target)
            outputs = {name: (target / name).read_bytes() for name in result['derived_streams']}
            checked = reopen_streams(raw, outputs, metadata)
            for name, details in result['derived_streams'].items():
                require(sha(target / name) == details['sha256'], 'Derivative hash differs from its receipt')
            state['rows'].append({'asset_id': case['asset_id'], 'capture_ordinal': ordinal,
                'source_mesh_identity': expected, 'derivative': str(target),
                'geometry_receipt_sha256': sha(target / 'GEOMETRY.v1.json'),
                'derived_streams_reopened': checked, 'vertices': metadata['vertices'],
                'submeshes': metadata['submeshes'], 'prefab_instance_aliases': case['prefab_instance_aliases'],
                'native_case_verified': True, 'whole_native_capture_complete': False,
                'original_raw_buffers_preserved': True, 'glb_created': False,
                'source_frame_note': 'Intermediate reflected source-UV tangent stream is not the unreflected Unity shader object frame; final source attributes must bind original captured vectors.',
                'material_render_qualified': False, 'full_import': False})
            (ROOT / 'RESULT.v1.json').write_text(json.dumps(state, indent=2), 'utf-8')
        for p, expected in pins.items():
            require(sha(p) == expected, 'Source changed during subset conversion: ' + p)
        state.update(status='20_verified_townsmith_geometry_derivatives_reopened', verified_meshes=20,
            input_pins=pins, unchanged_converter_reused=True,
            derived_streams_reopened=sum(row['derived_streams_reopened'] for row in state['rows']))
    except Exception as error:
        state.update(status='refused_or_partial', error=str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(state, indent=2), 'utf-8')
    print(json.dumps({k: v for k, v in state.items() if k not in ('rows', 'input_pins')}))
    if state['status'] == 'refused_or_partial':
        raise SystemExit(1)

if __name__ == '__main__':
    main()
