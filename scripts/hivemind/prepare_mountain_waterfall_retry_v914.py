"""Preserve failed IO receipts and prepare the missing capture directories."""
from pathlib import Path
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');O=W/'mountain-waterfall-typed-controls-v914'
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    prior=W/'mountain-waterfall-typed-controls-v912/PREPARED.v1.json'
    run_path=W/'mountain-waterfall-typed-controls-run-v913/RUN-RESULT.v1.json'
    run=json.loads(run_path.read_text('utf-8-sig'))
    assert run['status']=='native_waterfall_typed_controls_finished_requires_review' and run['unity_exit_code']==0 and not run['input_changes']
    assert len(run['refused_cases'])==6 and all('DirectoryNotFoundException' in r['error'] for r in run['refused_cases'])
    prep=json.loads(prior.read_text('utf-8-sig'))
    for p,d in prep['input_pins'].items():assert sha(p)==d,p
    O.mkdir(exist_ok=False);(O/'temp').mkdir()
    for case in prep['cases']:
        case['output']=case['output'].replace('mountain-waterfall-typed-controls-v912','mountain-waterfall-typed-controls-v914')
        Path(case['output']).parent.mkdir()
    manifest=O/'manifest.json';manifest.write_text(json.dumps(dict(cases=prep['cases'],output=str(O/'NATIVE-RESULT.v1.json')),indent=2),'utf-8')
    prep['manifest']=str(manifest)
    prep['input_pins'].update({str(prior):sha(prior),str(run_path):sha(run_path),str(manifest):sha(manifest),str(Path(__file__).resolve()):sha(__file__)})
    (O/'PREPARED.v1.json').write_text(json.dumps(prep,indent=2),'utf-8')
    print(json.dumps(dict(status='retry_prepared_after_verified_terminal_io_failure',cases=6,root=str(O))))
if __name__=='__main__':main()
