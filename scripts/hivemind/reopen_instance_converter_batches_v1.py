"""Check immutable receipts and generated shaders; do not imply asset delivery."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'source-instance-converter-batches-reopen-v1'
RECEIPTS = [W / 'delta-vector-bevy-vertex-stages-v3/RESULT.v1.json', W / 'delta-rocks-firepit-bevy-vertex-v2/RESULT.v1.json', W / 'native-hdrp-nine-source-programs-v1/source-graph-bevy-vertex-wrapper-v5/RESULT.v1.json']

def sha(p):
    with p.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()

def main():
    assert not ROOT.exists()
    pins = {str(Path(__file__).resolve()): sha(Path(__file__).resolve())}
    rows = []
    compositions = 0
    for p in RECEIPTS:
        d = json.loads(p.read_text('utf-8-sig'))
        assert not d['failures'] and not d['renderer_installed'] and not d['full_import']
        pins[str(p)] = sha(p)
        for group in ['input_pins', 'output_pins']:
            for path, expected in d.get(group, {}).items():
                assert sha(Path(path)) == expected, path
                assert path not in pins or pins[path] == expected
                pins[path] = expected
        for row in d['rows']:
            output = Path(row.get('shader') or row['forward_vertex'])
            assert sha(output) == row['sha256'], str(output)
            text = output.read_text('utf-8')
            assert 'source_graph_displaced_forward_frame(vertex,vertex.instance_index)' in text.replace(' ', '').replace('\n', '')
            stages = row.get('actual_stage_compositions') or row['actual_stage_permutations']
            assert len(stages) == 4 and all(s['actual_exit_code'] == 0 for s in stages)
            compositions += len(stages)
            pins[str(output)] = row['sha256']
            rows.append(dict(receipt=str(p), material_guid=row['material_guid'], shader=str(output), sha256=row['sha256']))
        for batch in d.get('batch_receipts', []):
            assert sha(Path(batch['path'])) == batch['sha256']
            pins[batch['path']] = batch['sha256']
    last = json.loads(RECEIPTS[-1].read_text('utf-8-sig'))
    refusals = last['unsupported_stage_refusal_tests']
    assert len(refusals) == 5 and all(r['actual_exit_code'] == 3 and r['source_specific_reason_present'] for r in refusals)
    assert len(rows) == 56 and compositions == 224
    result = dict(status='common_instance_converter_outputs_reopened_and_pinned', finished_utc=datetime.now(timezone.utc).isoformat(),
        generated_stage_variants_verified=len(rows), positive_compositions_verified=compositions,
        unsupported_feature_refusals_preserved=5, pin_count=len(pins), rows=rows, input_pins=pins,
        renderer_installed=False, full_import=False, gpu_qualified=False, vault_assets_updated=0, complete_assets_delivered_increment=0)
    ROOT.mkdir()
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps({k: v for k, v in result.items() if k not in ['rows', 'input_pins']}))

if __name__ == '__main__':
    main()
