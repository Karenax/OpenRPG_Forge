"""Keep the current lot state honest after a real compiler refusal and repair."""
from pathlib import Path
import hashlib
import json
import subprocess
import sys

S = Path(__file__).parent
W = Path('F:/OpenRPG_ImportWork')
T = W / 'SUIVI_LOTS_HIVEMIND'


def main():
    proof = W / 'townsmith-s-paint-material-host-adapter-v1/RESULT.v1.json'
    result = json.loads(proof.read_text(encoding='utf-8'))
    assert result['status'] == 'material_common_uniform_host_adapter_composed'
    for path, expected in result['input_pins'].items():
        assert hashlib.sha256(Path(path).read_bytes()).hexdigest() == expected, path
    path = S / 'record_batch_progress_v1.py'
    source = path.read_text(encoding='utf-8')
    assert 'host_adapter_close' not in source
    anchor = " active_lot=data.get('current_work_sub_lot',active_lot);active_state=data.get('current_work_state',active_state)"
    assert source.count(anchor) == 1
    block = """ host_adapter_close=read(W/'townsmith-s-paint-material-host-adapter-v1/RESULT.v1.json')
 if host_adapter_close.get('status')=='material_common_uniform_host_adapter_composed':
  recovery=read(W/'townsmith-ten-texture-engine-recovery-v1/RUN-RESULT.v1.json')
  engine_retry=read(W/'source-graph-ten-texture-engine-check-v2/RUN-RESULT.v1.json')
  install_retry=read(W/'native-texture-capture-common-install-v2/RUN-RESULT.v1.json')
  retry_state=install_retry.get('status',engine_retry.get('status',recovery.get('status','pending')))
  data['current_work_state']='Dix textures / 32 vues partagees / 134 operations conservees, 35 tests candidats passes ; shader arithmetique et adaptateur uniforms communs composes (2 controles chacun). Test moteur v1 refuse : constructeur non eclaire incomplet, corrige sans ajouter de textures. Reprise : '+retry_state+' ; transport UV0 complet, passes de rendu, sampling implicite et Studio restants'
  data['material_host_adapter_proof']=str(W/'townsmith-s-paint-material-host-adapter-v1/RESULT.v1.json')
  data['engine_recovery_proof']=str(W/'townsmith-ten-texture-engine-recovery-v1/RUN-RESULT.v1.json')
  key+=':'+host_adapter_close['status']+':'+retry_state
"""
    source = source.replace(anchor, block + anchor)
    compile(source, str(path), 'exec')
    path.write_text(source, encoding='utf-8')
    path = S / 'render_lot_dashboard_v1.py'
    source = path.read_text(encoding='utf-8')
    anchor = "    path = T / 'LOTS_ET_AVANCEMENT.md'"
    assert source.count(anchor) == 1
    block = "    host_adapter_proof_path=W/'townsmith-s-paint-material-host-adapter-v1/RESULT.v1.json'\n    output += '**Adaptateur du materiau et reprise moteur :** binding uniform 100, trois profils de normales et transforms explicites raccordes ; deux compositions passent. Le test moteur v1 a refuse un constructeur non eclaire incomplet ; trois slots optionnels initialises sans changer sa capacite de sept textures. Reprise sequentielle v2, refus v1 conserve. UV0 complet dans les varyings, passes completes et Studio restent ouverts. [Adaptateur](../townsmith-s-paint-material-host-adapter-v1/RESULT.v1.json), [reprise](../townsmith-ten-texture-engine-recovery-v1/RUN-RESULT.v1.json). Aucun asset complet ajoute.\\n\\n'\n"
    source = source.replace(anchor, block + anchor)
    anchor = "    meta = T / 'LOTS_ET_AVANCEMENT.v1.json'"
    assert source.count(anchor) == 1
    source = source.replace(anchor, "    manifest['input_pins'][str(host_adapter_proof_path)]=hashlib.sha256(host_adapter_proof_path.read_bytes()).hexdigest()\n" + anchor)
    compile(source, str(path), 'exec')
    path.write_text(source, encoding='utf-8')
    with (T / 'JOURNAL.md').open('a', encoding='utf-8') as handle:
        handle.write('\n- ' + result['finished_utc'] + ' — L03.TOWNSMITH.SHADER.S_PAINT.HOST : adaptateur uniform commun compose, 2 controles passes, entrees explicites conservees. Test moteur v1 termine en refus E0063 ; constructeur non eclaire corrige (slots 7/8/9 optionnels vides, capacite source sept inchangee). Reprise v2 en file derriere Cargo Studio actif ; aucun travail concurrent interrompu. [Adaptateur](../townsmith-s-paint-material-host-adapter-v1/RESULT.v1.json), [reprise](../townsmith-ten-texture-engine-recovery-v1/RUN-RESULT.v1.json). Rendu/Studio non qualifies, aucun asset complet ajoute.\n')
    subprocess.run([sys.executable, '-B', str(S / 'record_batch_progress_v1.py')], check=True, stdout=subprocess.DEVNULL)
    manifest = json.loads((T / 'LOTS_ET_AVANCEMENT.v1.json').read_text(encoding='utf-8-sig'))
    for path, expected in manifest['input_pins'].items():
        assert hashlib.sha256(Path(path).read_bytes()).hexdigest() == expected, path
    print('Host adapter and repaired engine retry recorded; dashboard pins verified.')


if __name__ == '__main__':
    main()
