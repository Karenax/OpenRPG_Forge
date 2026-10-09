from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,subprocess,sys
S=Path(__file__).parent;W=Path('F:/OpenRPG_ImportWork');T=W/'SUIVI_LOTS_HIVEMIND';ROOT=W/'native-texture-capture-contract-reopen-v1'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    check=W/'native-texture-capture-contract-check-v2';receipt=check/'RESULT.v1.json';d=read(receipt)
    assert d['compile_exit_code']==d['test_exit_code']==0
    for p,h in d['input_pins'].items():assert sha(p)==h,p
    log=(check/'tests.stdout.log').read_text();assert '8 passed; 0 failed' in log
    assert 'ten_actual_resource_chains_and_identity_sampler_refusals ... ok' in log
    resource=W/'townsmith-s-paint-native-resources-v1/RESOURCES.v1.json';r=read(resource)
    for p,h in r['input_pins'].items():assert sha(p)==h,p
    assert len(r['rows'])==10
    pins={str(p):sha(p) for p in [receipt,check/'validator.rs',check/'validator.exe',check/'compile.stdout.log',check/'compile.stderr.log',check/'tests.stdout.log',check/'tests.stderr.log',resource,Path(__file__)]}
    pins.update(d['input_pins'])
    result={'status':'isolated_native_capture_validator_reopened_eight_tests_ten_actual_resources','finished_utc':datetime.now(timezone.utc).isoformat(),
        'tests_passed':8,'actual_texture_resources_accepted':10,'mutated_resource_captures_refused':40,
        'input_pins':pins,'common_engine_installation_verified':False,'original_meta_identity_binding_verified':False,
        'forge_gpu_sampling_qualified':False,'forge_editor_verified':False,'full_import':False,'complete_assets_delivered_increment':0,
        'next_action':'Finish existing engine tests; bind native GUID to original meta, install common protocol support and rerun integration tests, then GPU comparisons'}
    ROOT.mkdir(exist_ok=False);proof=ROOT/'RESULT.v1.json';proof.write_text(json.dumps(result,indent=2),encoding='utf-8')
    p=S/'record_batch_progress_v1.py';s=p.read_text(encoding='utf-8');anchor=" active_lot=data.get('current_work_sub_lot',active_lot);active_state=data.get('current_work_state',active_state)";assert s.count(anchor)==1
    block=" candidate_close=read(W/'native-texture-capture-contract-reopen-v1/RESULT.v1.json')\n if candidate_close.get('status')=='isolated_native_capture_validator_reopened_eight_tests_ten_actual_resources':\n  data['current_work_state']+=' ; candidat admission isole : 8 tests passes, 10 ressources acceptees et 40 mutations refusees ; raccordement aux meta et installation commune restants'\n  data['native_capture_candidate_proof']=str(W/'native-texture-capture-contract-reopen-v1/RESULT.v1.json')\n  key+=':'+candidate_close['status']\n"
    p.write_text(s.replace(anchor,block+anchor),encoding='utf-8')
    p=S/'render_lot_dashboard_v1.py';s=p.read_text(encoding='utf-8');anchor="    output += 'Prochaine action enregistrée : '+state['next_action']+'.\\n\\n'";assert s.count(anchor)==1
    block="    candidate_close_path=W/'native-texture-capture-contract-reopen-v1/RESULT.v1.json'\n    output += '**Admission preparee et testee en isolation :** 8 tests passes ; 10 ressources reelles acceptees, 40 mutations refusees. Liaison GUID/meta, installation commune et sampling GPU restants. [Preuve limitee](../native-texture-capture-contract-reopen-v1/RESULT.v1.json).\\n\\n'\n"
    s=s.replace(anchor,block+anchor);anchor="    meta = T / 'LOTS_ET_AVANCEMENT.v1.json'";assert s.count(anchor)==1
    s=s.replace(anchor,"    manifest['input_pins'][str(candidate_close_path)]=hashlib.sha256(candidate_close_path.read_bytes()).hexdigest()\n"+anchor);p.write_text(s,encoding='utf-8')
    with (T/'JOURNAL.md').open('a',encoding='utf-8') as f:f.write('\n- '+result['finished_utc']+' — L03 admission : candidat isole, 8 tests passes, 10 ressources reelles acceptees et 40 mutations refusees. Installation commune, GUID/meta et GPU restent ouverts. [Preuve](../native-texture-capture-contract-reopen-v1/RESULT.v1.json). Aucun asset complet ajoute.\n')
    subprocess.run([sys.executable,'-B',str(S/'record_batch_progress_v1.py')],check=True,stdout=subprocess.DEVNULL)
    for p,h in read(T/'LOTS_ET_AVANCEMENT.v1.json')['input_pins'].items():assert sha(p)==h,p
    print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()
