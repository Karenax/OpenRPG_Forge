"""Seal actual parent-module candidate tests, keeping installation/rendering open."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,subprocess,sys
S=Path(__file__).parent;W=Path('F:/OpenRPG_ImportWork');T=W/'SUIVI_LOTS_HIVEMIND';ROOT=W/'native-texture-identity-contract-reopen-v2'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    root=W/'native-texture-identity-contract-check-v2';check=root/'RESULT.v1.json';d=read(check)
    prep=W/'native-texture-identity-contract-candidate-v2/PREPARED.v1.json';p=read(prep)
    assert d['compile_exit_code']==d['test_exit_code']==0
    for entries in [d['input_pins'],p['input_pins'],p['candidate_pins']]:
        for name,h in entries.items():assert sha(name)==h,name
    stdout=(root/'tests.stdout.log').read_text();assert '30 passed; 0 failed' in stdout
    assert 'ten_actual_resource_chains_and_identity_sampler_refusals ... ok' in stdout
    manifest=W/'townsmith-s-paint-native-resources-v1/RESOURCES.v1.json'
    for name,h in read(manifest)['input_pins'].items():assert sha(name)==h,name
    files=[check,prep,manifest,root/'validator.rs',root/'validator.exe',root/'compile.stdout.log',root/'compile.stderr.log',root/'tests.stdout.log',root/'tests.stderr.log',Path(__file__)]
    pins={str(p):sha(p) for p in files};pins.update(d['input_pins']);pins.update(p['input_pins'])
    result={'status':'real_common_parent_candidate_reopened_30_tests_ten_original_resources_guid_binding_verified',
        'finished_utc':datetime.now(timezone.utc).isoformat(),'tests_passed':30,'actual_resources_with_original_png_meta_accepted':10,
        'capture_identity_sampler_schema_restoration_mutations_refused':40,'original_meta_guid_mutations_refused':30,'failed_single_resource_rows_refused':10,'unknown_protocols_refused':2,'partial_single_resource_rows_accepted':10,'input_pins':pins,
        'candidate_native_guid_to_original_meta_binding_verified':True,'live_common_engine_installation_verified':False,
        'forge_gpu_sampling_qualified':False,'forge_editor_verified':False,'full_import':False,'complete_assets_delivered_increment':0,
        'scope':'Actual common module copies compiled with their original scalar modules; path adjustments and native protocol/GUID additions, row integrity independent of batch count/status; serialized legacy mip bias remains verbatim in meta, transport test uses measured effective bias',
        'next_action':'Finish engine binding tests, install common candidate without erasing originals, run workspace integration tests and GPU sampling/normal comparisons'}
    ROOT.mkdir(exist_ok=False);proof=ROOT/'RESULT.v1.json';proof.write_text(json.dumps(result,indent=2),encoding='utf-8')
    path=S/'record_batch_progress_v1.py';s=path.read_text(encoding='utf-8');anchor=" active_lot=data.get('current_work_sub_lot',active_lot);active_state=data.get('current_work_state',active_state)";assert s.count(anchor)==1
    block=" identity_close=read(W/'native-texture-identity-contract-reopen-v2/RESULT.v1.json')\n if identity_close.get('status')=='real_common_parent_candidate_reopened_30_tests_ten_original_resources_guid_binding_verified':\n  data['current_work_state']+=' ; contrat parent reel en isolation : 30 tests passes, GUID/meta verifies sur les 10 ressources originales, 80 mutations de ressources et deux protocoles inconnus refuses ; installation commune et GPU restants'\n  data['native_identity_candidate_proof']=str(W/'native-texture-identity-contract-reopen-v2/RESULT.v1.json')\n  data['next_action']='Terminer les tests moteur actifs, installer le contrat commun puis rejouer les tests du workspace et comparer sampling/normales sur GPU'\n  key+=':'+identity_close['status']\n"
    path.write_text(s.replace(anchor,block+anchor),encoding='utf-8')
    path=S/'render_lot_dashboard_v1.py';s=path.read_text(encoding='utf-8');anchor="    output += 'Prochaine action enregistrÃ©e : '+state['next_action']+'.\\n\\n'";assert s.count(anchor)==1
    s=s.replace(anchor,"    native_identity_proof_path=W/'native-texture-identity-contract-reopen-v2/RESULT.v1.json'\n    output += '**Contrat parent teste en isolation :** 30 tests passes, dix ressources originales avec PNG/meta acceptees ; 40 mutations de capture et 30 mutations des GUID metadata refusees. Installation dans le workspace, GPU et Studio restent ouverts. [Preuve](../native-texture-identity-contract-reopen-v2/RESULT.v1.json).\\n\\n'\n"+anchor)
    anchor="    meta = T / 'LOTS_ET_AVANCEMENT.v1.json'";assert s.count(anchor)==1
    s=s.replace(anchor,"    manifest['input_pins'][str(native_identity_proof_path)]=hashlib.sha256(native_identity_proof_path.read_bytes()).hexdigest()\n"+anchor);path.write_text(s,encoding='utf-8')
    planpath=T/'PLAN_EXECUTION_LOTS_20261009.v1.json';plan=read(planpath);plan['current_receipt']=str(proof);plan['updated_utc']=result['finished_utc'];plan['next_action']=result['next_action'];planpath.write_text(json.dumps(plan,indent=2,ensure_ascii=False),encoding='utf-8')
    with (T/'PLAN_EXECUTION_LOTS_20261009.md').open('a',encoding='utf-8') as f:f.write('\n- Contrat parent natif/GUID teste en isolation : 30 tests passes, dix ressources avec originaux/meta acceptees, 80 mutations de ressources et deux protocoles inconnus refuses. Installation, GPU et Studio restants. [Preuve](../native-texture-identity-contract-reopen-v2/RESULT.v1.json).\n')
    with (T/'JOURNAL.md').open('a',encoding='utf-8') as f:f.write('\n- '+result['finished_utc']+' â€” L03 admission : contrat parent reel en isolation, 30 tests passes ; liaison GUID/meta 10/10 ; refus de ressources 80/80 et deux protocoles inconnus (dont 30 meta rehashees). Sources du test moteur actif preservees ; installation et GPU restent ouverts. [Preuve](../native-texture-identity-contract-reopen-v2/RESULT.v1.json). Aucun nouvel asset complet.\n')
    subprocess.run([sys.executable,'-B',str(S/'record_batch_progress_v1.py')],check=True,stdout=subprocess.DEVNULL)
    for name,h in read(T/'LOTS_ET_AVANCEMENT.v1.json')['input_pins'].items():assert sha(name)==h,name
    print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','scope']}))
if __name__=='__main__':main()
