"""Resume the terminal transform checkpoint after a Windows sharing refusal."""
import datetime
import json
from pathlib import Path
import time

from reconcile_villa_scene_transforms import sha, document, reconcile, read

WORK = Path('F:/OpenRPG_ImportWork')
PRIOR = WORK/'villa-207-embedded-transform-reconciliation-v1'
LEAF = WORK/'villa-native-mesh-leaf-frame-reopen-v2/RESULT.v1.json'
ROOT = WORK/'villa-207-transform-reconciliation-resume-v2'


def save(name, value):
    target = ROOT/name
    temporary = target.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(value, indent=2, allow_nan=False), 'utf-8')
    for attempt in range(40):
        try:
            temporary.replace(target)
            return
        except PermissionError:
            if attempt == 39:
                raise
            time.sleep(0.25)


def main():
    assert not ROOT.exists(), 'Existing resume retained'
    old = read(PRIOR/'RUN-RESULT.v1.json')
    assert old['status'] == 'refused_or_partial' and 'WinError 5' in old['error']
    job_path = WORK/'SUIVI_LOTS_HIVEMIND/L04_VILLA_207_TRANSFORM_RECONCILIATION_JOB.v1.json'
    job = read(job_path)
    assert sha(Path(job['helper'])) == job['helper_sha256'], 'Original worker changed'
    inputs = read(PRIOR/'INPUTS.v1.json')
    by_id = {row['asset_id']: row for row in inputs}
    assert len(inputs) == len(by_id) == 207
    previous = {row['asset_id']: row for row in old['rows']}
    assert len(previous) == old['scenes_reconciled'] == 175
    assert old['nodes_reconciled'] == sum(r['nodes_reconciled'] for r in previous.values())
    for asset_id, row in previous.items():
        expected = by_id[asset_id]
        assert row['glb'] == expected['glb'] and row['glb_sha256'] == expected['sha256']
    leaf = read(LEAF)
    assert leaf['status'] == 'three_native_mesh_leaf_parent_frames_reopened'
    assert leaf['scenes_reconciled'] == 3 and not leaf['refusals'] and not leaf['changed_inputs']
    leaf_ids = {row['asset_id'] for row in leaf['rows']}
    assert len(leaf_ids) == 3 and leaf_ids == {r['asset_id'] for r in old['refusals']}
    assert all(r['reason'] == "'original_local_transform'" for r in old['refusals'])
    assert previous.keys().isdisjoint(leaf_ids)
    closed_inputs = []
    pins = {str(p): sha(p) for p in [Path(__file__).resolve(), Path(job['helper']), job_path,
                PRIOR/'RUN-RESULT.v1.json', PRIOR/'INPUTS.v1.json', LEAF]}
    for number in range(1, 12):
        folder = WORK/f'villa-native-source-frame-bound-l04-villa-{number:02}-v1'
        receipt_path, inputs_path = folder/'RUN-RESULT.v1.json', folder/'INPUTS.v1.json'
        receipt, batch_inputs = read(receipt_path), read(inputs_path)
        assert receipt['status'] == 'native_original_source_frames_bound_and_reopened'
        assert receipt['verified_scenes'] == len(batch_inputs) == (7 if number == 11 else 20)
        assert {r['asset_id'] for r in receipt['rows']} == {r['asset_id'] for r in batch_inputs}
        pins.update({str(receipt_path): sha(receipt_path), str(inputs_path): sha(inputs_path)})
        closed_inputs.extend(batch_inputs)
    assert closed_inputs == inputs, 'Original selected physical inputs changed'
    for path, expected in leaf['input_pins'].items():
        assert sha(Path(path)) == expected
        if path in pins:
            assert pins[path] == expected
        pins[path] = expected
    ROOT.mkdir()
    state = {'status': 'resuming_remaining_transform_checks', 'expected_scenes': 207,
             'previously_reconciled_scenes': 175, 'native_leaf_frames_reconciled': 3,
             'newly_reconciled_scenes': 0, 'rows': list(previous.values()),
             'native_leaf_rows': leaf['rows'], 'refusals': [],
             'glb_modified': False, 'observed_unity_world_qualified': False,
             'gpu_rendered': False, 'studio_verified': False, 'full_import': False}
    save('PREPARED.v1.json', {'input_pins': pins, 'remaining_ids': [r['asset_id'] for r in inputs
                            if r['asset_id'] not in previous and r['asset_id'] not in leaf_ids]})
    save('RUN-RESULT.v1.json', state)
    try:
        for row in inputs:
            if row['asset_id'] in previous or row['asset_id'] in leaf_ids:
                continue
            path = Path(row['glb'])
            assert sha(path) == row['sha256']
            try:
                result = reconcile(document(path))
                state['rows'].append({'asset_id': row['asset_id'], 'glb': str(path),
                                      'glb_sha256': row['sha256'], **result})
                state['newly_reconciled_scenes'] += 1
            except (AssertionError, KeyError, ValueError) as error:
                state['refusals'].append({'asset_id': row['asset_id'], 'reason': str(error)})
            assert sha(path) == row['sha256']
            save('RUN-RESULT.v1.json', state)
        state['status'] = 'reopening_final_closed_input_hashes'
        save('RUN-RESULT.v1.json', state)
        for row in inputs:
            pins[row['glb']] = row['sha256']
        for path, expected in pins.items():
            assert sha(Path(path)) == expected, 'Closed transform input changed: ' + path
        all_ids = {r['asset_id'] for r in state['rows']} | leaf_ids
        assert len(all_ids) == len(state['rows']) + 3
        assert all_ids | {r['asset_id'] for r in state['refusals']} == set(by_id)
        state.update(status=('207_embedded_and_native_leaf_transform_chains_reconciled'
                             if not state['refusals'] else 'remaining_transform_disagreements_retained'),
                     total_reconciled_scenes=len(all_ids), input_pins=pins,
                     all_input_pins_reopened=True,
                     finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    except Exception as error:
        state.update(status='refused_or_partial', error=str(error))
    save('RUN-RESULT.v1.json', state)
    print(json.dumps({k: v for k, v in state.items() if k not in ('rows', 'native_leaf_rows', 'input_pins')}))


if __name__ == '__main__':
    main()
