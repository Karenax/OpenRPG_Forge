"""Verify a frozen, already written native prefix without restarting Unity."""
import datetime
import importlib.util
import json
from pathlib import Path
import sys
import time

W = Path('F:/OpenRPG_ImportWork')
SOURCE = W / 'unity-nested-prefab-static-geometry-capture-v1/townsmith'
VERIFIER = W / 'unity-nested-prefab-static-geometry-remaining-v2/verify_export_v2.py'

def read(p):
    return json.loads(Path(p).read_text('utf-8-sig'))

def require(value, reason):
    if not value:
        raise RuntimeError(reason)

def progress_snapshot():
    for attempt in range(600):
        raw = (SOURCE / 'export/PROGRESS.json').read_bytes()
        try:
            return raw, json.loads(raw.decode('utf-8-sig'))
        except (json.JSONDecodeError, UnicodeDecodeError):
            if attempt == 599:
                raise
            time.sleep(1)

def main(start, count):
    require(start >= 0 and 0 < count <= 20, 'Bounded documented batch required')
    root = W / f'townsmith-native-partial-reopen-{start:04d}-{start+count-1:04d}-v2'
    require(not root.exists(), 'Existing batch evidence preserved')
    spec = importlib.util.spec_from_file_location('unchanged_native_mesh_verifier', VERIFIER)
    verifier = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(verifier)
    prepared_path = SOURCE / 'PREPARED.normalized-before-native.v2.json'
    prepared = read(prepared_path)
    manifest_path = SOURCE / 'manifest.json'
    manifest = read(manifest_path)
    raw, snapshot = progress_snapshot()
    require(snapshot['total'] == len(manifest['cases']) == len(prepared['inventory_rows']) == 893,
            'Expected original native mesh inventory')
    require(len(snapshot['cases']) >= start + count, 'Requested cases have not been written')
    selected = snapshot['cases'][start:start + count]
    selected_ids = {case['asset_id'] for case in selected}
    require(len(selected_ids) == count, 'Duplicate native cases')
    # Do not recheck batches already evidenced, even if their capture is still active.
    for p in W.glob('townsmith-native-partial-reopen-*-v*/RESULT.v1.json'):
        old = read(p)
        if old.get('status') != 'closed_native_townsmith_cases_independently_reopened':
            continue
        require(not selected_ids.intersection(r['asset_id'] for r in old.get('rows', [])), 'Earlier verified cases must not be repeated')
    root.mkdir()
    (root / 'NATIVE-PROGRESS-SNAPSHOT.json').write_bytes(raw)
    state = {'schema': 'openrpg.native-static-mesh-partial-batch-reopen.v1',
        'status': 'verifying_closed_native_cases', 'pack_id': prepared['pack_id'],
        'started_utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
        'start_ordinal': start, 'expected_cases': count, 'original_capture_expected_cases': 893,
        'snapshot_cases_written': len(snapshot['cases']), 'rows': [],
        'whole_original_batch_qualified': False, 'geometry_converted': False,
        'native_capture_rerun': False, 'studio_verified': False, 'full_import': False}
    try:
        pins = dict(prepared['source_proof_pins'])
        for p in [prepared_path, manifest_path, VERIFIER, Path(__file__).resolve(), root / 'NATIVE-PROGRESS-SNAPSHOT.json']:
            pins[str(p)] = verifier.sha(p)
        require(pins[str(manifest_path)] == prepared['manifest_sha256'], 'Original manifest changed')
        copies = {Path(c['project_copy']).resolve(): c for c in prepared['copies']}
        for ordinal in range(start, start + count):
            request = manifest['cases'][ordinal]
            copy = copies[(SOURCE / 'project' / request['asset_path']).resolve()]
            for field in ['original', 'original_meta', 'project_copy']:
                pins[copy[field]] = copy[field + '_sha256']
        for p, expected in pins.items():
            require(verifier.sha(p) == expected, 'Original input changed: ' + p)
        for ordinal in range(start, start + count):
            request = manifest['cases'][ordinal]
            captured = snapshot['cases'][ordinal]
            inventory = prepared['inventory_rows'][ordinal]
            formats = [f"{f['attribute']}:{f['format']}:{f['dimension']}:{f['stream']}" for f in captured['vertex_formats']]
            require(formats == inventory['original_vertex_formats'] and captured['vertices'] == inventory['vertices'],
                    'Native vertex formats/count differ from pinned hierarchy')
            require(captured['actual_guid'] == inventory['source']['guid'] and captured['actual_file_id'] == inventory['source']['file_id'],
                    'Original exact source identity changed')
            result = verifier.check_case(request, captured, SOURCE / 'export' / f'{ordinal:03d}')
            for name, expected in result['buffers'].items():
                pins[str(SOURCE / 'export' / f'{ordinal:03d}' / name)] = expected
            result.update(native_case_ordinal=ordinal, source_formats_verified=True,
                prefab_instance_aliases=inventory['aliases'], capture_snapshot=str(root / 'NATIVE-PROGRESS-SNAPSHOT.json'))
            state['rows'].append(result)
        _, current = progress_snapshot()
        require(current['cases'][start:start+count] == selected, 'Closed native rows changed while reopening')
        for p, expected in pins.items():
            require(verifier.sha(p) == expected, 'Physical source/buffer changed during verification: ' + p)
        require(len(state['rows']) == count, 'Incomplete selected batch')
        state.update(status='closed_native_townsmith_cases_independently_reopened', verified_native_mesh_cases=count,
            original_selected_sources_unchanged=True, unchanged_per_case_verifier_reused=True,
            captured_buffers_rehashed=sum(len(row['buffers']) for row in state['rows']), input_pins=pins)
    except Exception as error:
        state.update(status='refused_or_partial', error=str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    (root / 'RESULT.v1.json').write_text(json.dumps(state, indent=2), 'utf-8')
    print(json.dumps({k: v for k, v in state.items() if k not in ('rows', 'input_pins')}))
    if state['status'] == 'refused_or_partial':
        raise SystemExit(1)

if __name__ == '__main__':
    main(int(sys.argv[1]), int(sys.argv[2]))
