"""Seal the limited GPU result and preserve pre-install test source identities."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import subprocess
import sys

S = Path(__file__).parent
W = Path('F:/OpenRPG_ImportWork')
T = W / 'SUIVI_LOTS_HIVEMIND'
ROOT = W / 'townsmith-s-paint-native-pipeline-reopen-v1'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    assert not ROOT.exists()
    gpu = W / 'townsmith-s-paint-native-pipeline-v2'
    run, native, prepared = [read(gpu / n) for n in ('RUN-RESULT.v1.json', 'NATIVE-RESULT.v1.json', 'PREPARED.v1.json')]
    assert run['status'] == native['status'] == 'standalone_native_pipeline_admission_passed'
    assert run['compile_exit_code'] == run['gpu_exit_code'] == 0
    assert run['native_receipt_sha256'] == sha(gpu / 'NATIVE-RESULT.v1.json')
    assert native['pipelines_created'] == 7 and len(native['rows']) == 7
    assert native['backend'] == 'Dx12' and all(r['pipeline_created'] and r['validation_error'] is None for r in native['rows'])
    pins = dict(prepared['input_pins'])
    pins.update({str(gpu / n): sha(gpu / n) for n in ('RUN-RESULT.v1.json', 'NATIVE-RESULT.v1.json', 'PREPARED.v1.json', 'gpu.stdout.log', 'gpu.stderr.log')})
    install = W / 'native-texture-capture-common-install-v4'
    epoch = read(install / 'PREPARED.v1.json')
    engine = W / 'source-graph-ten-texture-engine-check-v3'
    engine_run, engine_prepared = read(engine / 'RUN-RESULT.v1.json'), read(engine / 'PREPARED.v1.json')
    assert engine_run['status'] == 'ten_texture_engine_binding_tests_passed_inputs_unchanged'
    assert engine_run['actual_exit_code'] == 0 and engine_run['tests_passed'] == 103
    preinstall_pins = {}
    substitutions = {}
    for path, expected in engine_prepared['input_pins'].items():
        resolved = path
        if sha(path) != expected:
            resolved = epoch['current_source_snapshots'][path]
            substitutions[path] = {'snapshot': resolved, 'historical_sha256': expected}
        assert sha(resolved) == expected, path
        preinstall_pins[resolved] = expected
    pins.update(preinstall_pins)
    for label in ('source_graph', 'source_unlit_forward', 'source_graph_program', 'source_graph_glb'):
        for suffix in ('stdout.log', 'stderr.log', 'COMMAND.v1.json'):
            path = engine / (label + '.' + suffix)
            pins[str(path)] = sha(path)
    for path in (engine / 'RUN-RESULT.v1.json', engine / 'PREPARED.v1.json', install / 'PREPARED.v1.json', Path(__file__)):
        pins[str(path)] = sha(path)
    for path, expected in pins.items():
        assert sha(path) == expected, path
    ROOT.mkdir()
    frozen = ROOT / 'PREINSTALL-ENGINE-SOURCE-EPOCH.v1.json'
    frozen.write_text(json.dumps({'input_pins': preinstall_pins, 'historical_source_substitutions': substitutions, 'tests_passed': 103,
                                 'scope': engine_run['scope'], 'broad_engine_suite_qualified': False}, indent=2), encoding='utf-8')
    pins[str(frozen)] = sha(frozen)
    result = {'status': 'seven_standalone_native_pipeline_admissions_reopened', 'finished_utc': datetime.now(timezone.utc).isoformat(),
              'pipelines_created': 7, 'adapter': native['adapter'], 'backend': native['backend'], 'preinstall_engine_tests_passed': 103,
              'input_pins': pins, 'actual_bevy_bind_group_admission_qualified': False, 'source_pipeline_state_qualified': False,
              'gpu_draw_executed': False, 'source_unity_render_parity_qualified': False,
              'forge_editor_verified': False, 'full_import': False, 'complete_assets_delivered_increment': 0}
    proof = ROOT / 'RESULT.v1.json'
    proof.write_text(json.dumps(result, indent=2), encoding='utf-8')
    path = S / 'record_batch_progress_v1.py'
    source = path.read_text(encoding='utf-8')
    source = source.replace("install_retry=read(W/'native-texture-capture-common-install-v3/RUN-RESULT.v1.json')", "install_retry=read(W/'native-texture-capture-common-install-v4/RUN-RESULT.v1.json')")
    source = source.replace("data['current_engine_check_proof']=str(W/'source-graph-ten-texture-engine-check-v2/RUN-RESULT.v1.json')", "data['current_engine_check_proof']=str(W/'source-graph-ten-texture-engine-check-v3/RUN-RESULT.v1.json')")
    source = source.replace("data['common_install_queue']=str(W/'native-texture-capture-common-install-v3/RUN-RESULT.v1.json')", "data['common_install_queue']=str(W/'native-texture-capture-common-install-v4/RUN-RESULT.v1.json')")
    assert 'townsmith_pipeline_close' not in source
    anchor = " active_lot=data.get('current_work_sub_lot',active_lot);active_state=data.get('current_work_state',active_state)"
    assert source.count(anchor) == 1
    block = " townsmith_pipeline_close=read(W/'townsmith-s-paint-native-pipeline-reopen-v1/RESULT.v1.json')\n if townsmith_pipeline_close.get('pipelines_created')==7:\n  current_install=read(W/'native-texture-capture-common-install-v4/RUN-RESULT.v1.json')\n  data['current_work_state']='103 tests moteur cibles passent (93 source_graph, 4 unlit, 6 GLB ; module program sans test propre). Sept pipelines autonomes D3D12 crees et reouverts. Contrat commun applique, controles post-installation : '+current_install.get('status','pending')+' ; bindings Bevy effectifs, etat pipeline source, rendu GPU/fidelite HDRP, sampling implicite et Studio restants'\n  data['townsmith_pipeline_admission_proof']=str(W/'townsmith-s-paint-native-pipeline-reopen-v1/RESULT.v1.json')\n  key+=':'+townsmith_pipeline_close['status']+':'+current_install.get('status','pending')\n"
    source = source.replace(anchor, block + anchor)
    compile(source, str(path), 'exec')
    path.write_text(source, encoding='utf-8')
    path = S / 'render_lot_dashboard_v1.py'
    source = path.read_text(encoding='utf-8')
    anchor = "    path = T / 'LOTS_ET_AVANCEMENT.md'"
    assert source.count(anchor) == 1
    source = source.replace(anchor, "    townsmith_pipeline_proof_path=W/'townsmith-s-paint-native-pipeline-reopen-v1/RESULT.v1.json'\n    output += '**Admission GPU native autonome :** sept pipelines D3D12 crees et reouverts sur RX 7800 XT, layouts inferes ; 103 tests moteur cibles passent, cinq refus Tidewater du controle elargi restent conserves. Contrat commun applique, tests apres installation en cours. Bindings Bevy effectifs, draw/readback et fidelite Unity/Studio restent ouverts. [Preuve](../townsmith-s-paint-native-pipeline-reopen-v1/RESULT.v1.json), [installation](../native-texture-capture-common-install-v4/RUN-RESULT.v1.json). Aucun asset complet ajoute.\\n\\n'\n" + anchor)
    anchor = "    meta = T / 'LOTS_ET_AVANCEMENT.v1.json'"
    assert source.count(anchor) == 1
    source = source.replace(anchor, "    manifest['input_pins'][str(townsmith_pipeline_proof_path)]=hashlib.sha256(townsmith_pipeline_proof_path.read_bytes()).hexdigest()\n" + anchor)
    compile(source, str(path), 'exec')
    path.write_text(source, encoding='utf-8')
    with (T / 'JOURNAL.md').open('a', encoding='utf-8') as stream:
        stream.write('\n- ' + result['finished_utc'] + ' — L03.TOWNSMITH.SHADER.S_PAINT.PIPELINE : sept pipelines autonomes D3D12 crees et reouverts sur RX 7800 XT ; aucun draw effectue. 103 tests moteur cibles passent, snapshots des sources avant installation conserves ; cinq refus Tidewater du controle elargi restent non qualifies. Contrat capture native applique, tests post-installation en cours. [Preuve](../townsmith-s-paint-native-pipeline-reopen-v1/RESULT.v1.json). Bindings Bevy, rendu et Studio restants ; aucun asset complet ajoute.\n')
    subprocess.run([sys.executable, '-B', str(S / 'record_batch_progress_v1.py')], check=True, stdout=subprocess.DEVNULL)
    print(json.dumps({k: v for k, v in result.items() if k != 'input_pins'}))


if __name__ == '__main__':
    main()
