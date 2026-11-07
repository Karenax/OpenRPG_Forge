"""Seal successful SDK controls individually, retaining every refused card."""
from pathlib import Path
import hashlib,json
from datetime import datetime,timezone

W=Path('F:/OpenRPG_ImportWork')
O=W/'mountain-sdk-partial-review-v911'
def read(p): return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f: return hashlib.file_digest(f,'sha256').hexdigest()

def main():
    run_path=W/'mountain-native-sdk-derivation-run-v909/RUN-RESULT.v1.json'
    prep_path=W/'mountain-native-parameter-comparison-v908/PREPARED.v1.json'
    run,prep=read(run_path),read(prep_path)
    assert run['status']=='native_sdk_derivation_finished_requires_scope_review'
    assert run['unity_exit_code']==0 and not run['input_changes']
    pins={str(run_path):sha(run_path),str(prep_path):sha(prep_path)}
    for p,digest in run['output_pins'].items():
        assert sha(p)==digest,p
        pins[p]=digest
    passed,held=[],[]
    for offset in range(0,len(prep['cases']),6):
        cases=prep['cases'][offset:offset+6]
        failures=[r for r in run['refused_cases'] if r['id'] in {c['id'] for c in cases}]
        if failures:
            held.append(dict(id=cases[0]['source_card_id'],failures=failures));continue
        reports=[read(c['output']) for c in cases]
        assert all(len(r['images'])==2 for r in reports)
        expected={e['guid'] for e in cases[3]['comparisons']}
        for report in reports[3:5]:
            observations=report['material_comparisons']
            assert {e['guid'] for e in observations}==expected
            assert all(e['sdk_derived_state_applied'] and e['authored_inputs_preserved'] and e['active_author_float_count']>0 and e['sdk_material_state'] for e in observations)
        assert all(r['selected_mesh_identities']==reports[1]['selected_mesh_identities'] and r['selected_mesh_vertex_counts']==reports[1]['selected_mesh_vertex_counts'] for r in reports)
        for view in range(2):
            assert len({reports[i]['images'][view]['raw_sha256'] for i in range(1,6)})==1
        passed.append(dict(id=cases[0]['source_card_id'],native_receipts=[c['output'] for c in cases],views_verified=2))
    assert len(passed)==46 and len(held)==1
    O.mkdir(exist_ok=False)
    result=dict(status='46_mountain_cards_sdk_author_controls_raw_exact_one_card_refused',finished_utc=datetime.now(timezone.utc).isoformat(),cards_verified=len(passed),views_verified=2*len(passed),rows=passed,held=held,input_pins=pins,images_admitted=0,original_sdk_pipeline_equivalence=False,full_import=False)
    (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:result[k] for k in ['status','cards_verified','views_verified','held']}))

if __name__=='__main__':main()
