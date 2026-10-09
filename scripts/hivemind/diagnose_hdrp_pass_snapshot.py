"""Retain the actual failed capture and inspect original graph target types."""
import collections
import hashlib
import json
from pathlib import Path
import re

WORK = Path('F:/OpenRPG_ImportWork')
ROOT = WORK / 'dungeon-native-pass-snapshot-diagnosis-v1'
CAPTURE = WORK / 'unity-dungeon-original-shadergraph-hdrp-context-v3/NATIVE-RESULT.v3.json'
RUN = WORK / 'unity-dungeon-original-shadergraph-hdrp-context-v3/RUN-RESULT.v3.json'
PREPARED = WORK / 'unity-dungeon-original-shadergraph-compiler-v1/PREPARED.v1.json'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    assert not ROOT.exists(), 'Existing diagnosis preserved'
    inputs = {str(path): sha(path) for path in [CAPTURE, RUN, PREPARED, Path(__file__).resolve()]}
    capture = json.loads(CAPTURE.read_text('utf-8-sig'))
    run = json.loads(RUN.read_text('utf-8-sig'))
    prepared = json.loads(PREPARED.read_text('utf-8-sig'))
    assert run['status'] == 'refused' and run['actual_unity_exit_code'] == 1
    assert capture['requested'] == 42 and capture['captured'] == 3 and capture['failed'] == 39
    graphs = []
    for copy in prepared['copies']:
        path = Path(copy['original'])
        if path.suffix.lower() != '.shadergraph':
            continue
        assert sha(path) == copy['original_sha256'], 'Original graph changed'
        raw = path.read_text('utf-8-sig')
        types = sorted(set(re.findall(r'"m_Type"\s*:\s*"([^"]+)"', raw)))
        graphs.append({'path': str(path), 'sha256': copy['original_sha256'],
            'serialized_target_or_master_types': [value for value in types
                if any(token in value for token in ['Target', 'Master', 'SubTarget'])]})
        inputs[str(path)] = copy['original_sha256']
    failures = collections.Counter((row['shader_name'], row['shader_guid'])
                                   for row in capture['cases'] if row['error'])
    successful = [{'material_guid': row['material_guid'], 'shader_name': row['shader_name'],
                   'passes': row['passes'], 'hdrp_render_parity_qualified': False}
                  for row in capture['cases'] if not row['error']]
    for path, expected in inputs.items():
        assert sha(path) == expected, 'Diagnosis input changed'
    result = {'status': '39_unnamed_pass_refusals_retained_pipeline_selection_to_reconcile',
        'requested_materials': 42, 'capture_report_successful_rows': 3,
        'capture_report_failed_rows': 39,
        'failures_by_shader': [{'shader_name': name, 'shader_guid': guid, 'materials': count}
                              for (name, guid), count in failures.items()],
        'successful_rows': successful, 'original_graph_targets': graphs,
        'input_pins_reopened': inputs, 'unnamed_pass_guard_relaxed': False,
        'original_pipeline_compatibility_proven': False,
        'hdrp_render_parity_qualified': False, 'full_import': False}
    ROOT.mkdir()
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2) + '\n', 'utf-8')
    print(json.dumps({key: value for key, value in result.items()
                     if key not in ['input_pins_reopened', 'successful_rows', 'original_graph_targets']}))
    print(json.dumps(graphs, ensure_ascii=False))


if __name__ == '__main__':
    main()
