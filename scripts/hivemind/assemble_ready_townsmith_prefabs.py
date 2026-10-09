"""Bind only complete prefab geometry closures; retain every source node/state."""
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import datetime

WORK = Path('F:/OpenRPG_ImportWork')
ROOT = WORK / 'townsmith-closed-prefab-bindings-v1'
HELPER = Path(__file__).with_name('assemble_nested_prefab_bindings.py')


def main():
    assert not ROOT.exists(), 'Preserve prior assembly'
    spec = importlib.util.spec_from_file_location('original_binder', HELPER)
    binder = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(binder)
    proof_path = binder.HIERARCHY / 'HIERARCHY-REOPEN.v1.json'
    capture_path = binder.HIERARCHY / 'projects/townsmith.HIERARCHY-RESULT.v1.json'
    proof = binder.read(proof_path)
    assert proof['source_and_capture_pins_unchanged_before_after']
    assert binder.sha(capture_path) == proof['proof_pins'][str(capture_path)]
    captured = binder.read(capture_path)
    assert captured['captured'] == captured['total'] == 8 and captured['failed'] == 0
    assert not captured['purchased_scripts_copied_or_executed']
    pins = {str(p): binder.sha(p) for p in (HELPER, Path(__file__), proof_path, capture_path)}
    controls = subprocess.run([sys.executable, str(HELPER), 'self-test'],
                              capture_output=True, text=True, check=True)
    passed = json.loads(controls.stdout)['assembly_controls_passed']
    assert len(passed) == 7
    index = {}
    banks = {}
    for path in sorted(WORK.glob('townsmith-native-geometry-*-v1/RESULT.v1.json')):
        try:
            bank = binder.read(path)
        except json.JSONDecodeError:
            continue
        if bank.get('status') not in ('20_verified_townsmith_geometry_derivatives_reopened',
                                      'verified_townsmith_geometry_derivatives_reopened'):
            continue
        assert bank['pack_id'] == 'hivemind.townsmith'
        assert bank['verified_meshes'] == bank['expected_meshes'] == len(bank['rows'])
        native_receipts = [Path(p) for p in bank['input_pins']
                           if 'townsmith-native-partial-reopen-' in p
                           and Path(p).name == 'RESULT.v1.json']
        assert len(native_receipts) == 1
        native_path = native_receipts[0]
        assert binder.sha(native_path) == bank['input_pins'][str(native_path)]
        native = binder.read(native_path)
        assert native['status'] == 'closed_native_townsmith_cases_independently_reopened'
        assert native['verified_native_mesh_cases'] == bank['verified_meshes']
        assert {r['asset_id'] for r in native['rows']} == {r['asset_id'] for r in bank['rows']}
        banks[str(path)] = bank
        for row in bank['rows']:
            ref = row['source_mesh_identity']
            key = ref['guid'], ref['file_id']
            assert key not in index and row['original_raw_buffers_preserved']
            if 'native_case_verified' in row:
                assert row['native_case_verified']
            index[key] = (row, str(path))
    selected, waiting = [], []
    used = {}
    for case in captured['cases']:
        static = [(node, renderer) for node in case['nodes'] for renderer in node['renderers']
                  if renderer.get('static_mesh_adapter_applicable')]
        missing = {binder.identity(renderer['mesh']['source']) for _, renderer in static
                   if binder.identity(renderer['mesh']['source']) not in index}
        if missing:
            waiting.append({'asset_id': case['asset_id'], 'missing_unique_meshes': len(missing)})
            continue
        assert static and not case['error']
        bindings = []
        for node, renderer in static:
            key = binder.identity(renderer['mesh']['source'])
            derivative, bank_path = index[key]
            bindings.append(binder.bind_renderer(case['asset_id'], node, renderer, derivative))
            used[key] = derivative
            if bank_path not in pins:
                pins[bank_path] = binder.sha(bank_path)
                for path, expected in banks[bank_path]['input_pins'].items():
                    assert binder.sha(path) == expected
                    if path in pins:
                        assert pins[path] == expected
                    pins[path] = expected
        selected.append({'asset_id': case['asset_id'], 'pack_id': 'hivemind.townsmith',
                         'nodes': [{'source_node': node,
                                    'target_local_matrix_column_major': binder.reflected_column_major(node['source_local_matrix_row_major']),
                                    'target_source_world_matrix_column_major': binder.reflected_column_major(node['source_local_to_world_matrix_row_major'])}
                                   for node in case['nodes']],
                         'static_renderer_bindings': bindings,
                         'deferred_renderers': [{'node_path': node['path'], 'renderer': renderer,
                                                'reason': 'requires_own_rig_or_effect_adapter'}
                                               for node in case['nodes'] for renderer in node['renderers']
                                               if not renderer.get('static_mesh_adapter_applicable')],
                         'original_lod_groups': case['lod_groups'], 'original_root': case['root'],
                         'original_components_colliders_inactive_nodes_preserved': True})
    assert selected
    for path, expected in proof['proof_pins'].items():
        if '/townsmith/' in path.replace('\\', '/').lower():
            assert binder.sha(path) == expected
            if path in pins:
                assert pins[path] == expected
            pins[path] = expected
    for derivative in used.values():
        path = Path(derivative['derivative']) / 'GEOMETRY.v1.json'
        assert binder.sha(path) == derivative['geometry_receipt_sha256']
        pins[str(path)] = derivative['geometry_receipt_sha256']
        for name, stream in binder.read(path)['derived_streams'].items():
            stream_path = path.parent / name
            assert binder.sha(stream_path) == stream['sha256']
            pins[str(stream_path)] = stream['sha256']
    for path, expected in pins.items():
        assert binder.sha(path) == expected, 'Assembly input changed'
    result = {'schema': 'openrpg.closed-townsmith-prefab-bindings.v1',
              'status': 'complete_geometry_prefab_bindings_reopened_materials_unqualified',
              'finished_utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
              'prefabs_bound': len(selected), 'unique_geometry_derivatives': len(used),
              'static_renderer_bindings': sum(len(r['static_renderer_bindings']) for r in selected),
              'material_draw_mapping_mismatches': sum(b['material_draw_mapping_requires_native_qualification']
                                                     for r in selected for b in r['static_renderer_bindings']),
              'rows': selected, 'waiting_prefabs': waiting, 'source_pins': pins,
              'assembly_controls_passed': passed, 'source_and_derivative_pins_unchanged': True,
              'glb_created': False, 'material_render_qualified': False,
              'studio_verified': False, 'full_import': False}
    ROOT.mkdir()
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2, allow_nan=False), 'utf-8')
    print(json.dumps({k: v for k, v in result.items() if k not in ('rows', 'source_pins')}))


if __name__ == '__main__':
    main()
