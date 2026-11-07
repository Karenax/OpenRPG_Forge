"""Seal actual typed author guards, repeats and SDK image comparisons."""
from pathlib import Path
import hashlib,json
from datetime import datetime,timezone
W=Path('F:/OpenRPG_ImportWork');O=W/'mountain-waterfall-typed-review-v916'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    run_path=W/'mountain-waterfall-typed-controls-run-v915/RUN-RESULT.v1.json'
    prep_path=W/'mountain-waterfall-typed-controls-v914/PREPARED.v1.json'
    run,prep=read(run_path),read(prep_path)
    assert run['status']=='native_capture_refusal_preserved' and run['error']=='AssertionError()'
    assert run['unity_exit_code']==0 and not run['input_changes']
    pins={str(run_path):sha(run_path),str(prep_path):sha(prep_path)}
    for p,d in prep['input_pins'].items():assert sha(p)==d,p;pins[p]=d
    batch_path=Path(prep['manifest']).parent/'NATIVE-RESULT.v1.json'
    batch=read(batch_path);pins[str(batch_path)]=sha(batch_path)
    assert len(batch['cases'])==6
    for case,actual in zip(prep['cases'],batch['cases']):
        assert actual['id']==case['id'] and actual['captured'] and actual['receipt']==case['output']
        report=read(case['output']);pins[case['output']]=sha(case['output'])
        assert report['source_object_unchanged'] and report['gpu']=='Direct3D11'
        assert report['prefab_guid']==case['prefab_guid'] and report['requested_lod']==case['requested_lod'] and report['requested_lod_selection_verified']
        assert report['source_lod_groups']==case['expected_lod_groups'] and report['source_empty_lod_slots']==case['expected_empty_lod_slots']
        assert len(report['images'])==2
        for image in report['images']:
            for path_key,digest_key in [('path','sha256'),('raw_path','raw_sha256')]:
                p=image[path_key];assert sha(p)==image[digest_key];pins[p]=image[digest_key]
            assert Path(image['raw_path']).stat().st_size==512*512*8
    reports=[read(c['output']) for c in prep['cases']]
    expected={e['guid'] for e in prep['cases'][3]['comparisons']}
    for report in reports[3:5]:
        assert {e['guid'] for e in report['material_comparisons']}==expected
        assert all(e['sdk_derived_state_applied'] and e['authored_inputs_preserved'] and e['sdk_material_state'] for e in report['material_comparisons'])
    assert all(r['selected_mesh_identities']==reports[1]['selected_mesh_identities'] and r['selected_mesh_vertex_counts']==reports[1]['selected_mesh_vertex_counts'] for r in reports)
    rows=[]
    for view in range(2):
        hashes=[reports[i]['images'][view]['raw_sha256'] for i in range(1,6)]
        assert hashes[0]==hashes[1]==hashes[4] and hashes[2]==hashes[3]
        rows.append(dict(view=view,original_author_sdk_reconstruction_raw_equal=hashes[0]==hashes[2]))
    result=dict(status='waterfall_typed_author_guards_and_native_repeats_verified',finished_utc=datetime.now(timezone.utc).isoformat(),controller_failure_preserved=True,independent_native_receipt_validation=True,cards_verified=1,views_verified=2,comparisons=rows,all_sdk_reconstructed_views_raw_exact=all(r['original_author_sdk_reconstruction_raw_equal'] for r in rows),source_id=prep['cases'][0]['source_card_id'],input_pins=pins,images_admitted=0,full_import=False,original_sdk_pipeline_equivalence=False)
    O.mkdir(exist_ok=False);(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:result[k] for k in ['status','cards_verified','comparisons','all_sdk_reconstructed_views_raw_exact']}))
if __name__=='__main__':main()
