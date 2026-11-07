"""Record real stage composition without promoting GPU or Studio qualification."""
from pathlib import Path
import hashlib
import json
import subprocess
import sys

S = Path(__file__).parent
W = Path('F:/OpenRPG_ImportWork')
T = W / 'SUIVI_LOTS_HIVEMIND'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    proof = W / 'townsmith-s-paint-lit-fragments-v1/RESULT.v1.json'
    result = json.loads(proof.read_text(encoding='utf-8'))
    assert result['status'] == 'townsmith_original_arithmetic_native_lit_and_prepass_composed'
    assert result['compositions_passed'] == 7
    for path, expected in result['input_pins'].items():
        assert sha(path) == expected, path
    path = S / 'record_batch_progress_v1.py'
    source = path.read_text(encoding='utf-8')
    assert 'townsmith_lit_close' not in source
    anchor = " active_lot=data.get('current_work_sub_lot',active_lot);active_state=data.get('current_work_state',active_state)"
    assert source.count(anchor) == 1
    block = " townsmith_lit_close=read(W/'townsmith-s-paint-lit-fragments-v1/RESULT.v1.json')\n if townsmith_lit_close.get('compositions_passed')==7:\n  data['current_work_state']=data['current_work_state'].replace('fragments eclaires, execution GPU, sampling implicite et Studio restants','fragments Lit/prepasse candidats composes pour sept variantes ; layout/pipeline GPU, fidelite HDRP, sampling implicite et Studio restants')\n  data['townsmith_lit_fragment_proof']=str(W/'townsmith-s-paint-lit-fragments-v1/RESULT.v1.json')\n  data['next_action']='Terminer les tests moteur et installation gardee ; qualifier layout/pipeline GPU effectifs et les bindings du materiau complet, comparer rendu et sampling a Unity HDRP, puis verifier Studio'\n  key+=':'+townsmith_lit_close['status']\n"
    source = source.replace(anchor, block + anchor)
    compile(source, str(path), 'exec')
    path.write_text(source, encoding='utf-8')
    path = S / 'render_lot_dashboard_v1.py'
    source = path.read_text(encoding='utf-8')
    anchor = "    path = T / 'LOTS_ET_AVANCEMENT.md'"
    assert source.count(anchor) == 1
    block = "    townsmith_lit_proof_path=W/'townsmith-s-paint-lit-fragments-v1/RESULT.v1.json'\n    output += '**Fragments Lit et prepasse candidats :** sept compositions passent avec dix slots texture et UV0 float4 ; 134 operations originales conservees, licence Bevy copiee. Layout/pipeline GPU, fidelite HDRP, sampling implicite et Studio restent ouverts. [Preuve](../townsmith-s-paint-lit-fragments-v1/RESULT.v1.json). Aucun asset complet ajoute.\\n\\n'\n"
    source = source.replace(anchor, block + anchor)
    anchor = "    meta = T / 'LOTS_ET_AVANCEMENT.v1.json'"
    assert source.count(anchor) == 1
    source = source.replace(anchor, "    manifest['input_pins'][str(townsmith_lit_proof_path)]=hashlib.sha256(townsmith_lit_proof_path.read_bytes()).hexdigest()\n" + anchor)
    compile(source, str(path), 'exec')
    path.write_text(source, encoding='utf-8')
    with (T / 'JOURNAL.md').open('a', encoding='utf-8') as stream:
        stream.write('\n- ' + result['finished_utc'] + ' — L03.TOWNSMITH.SHADER.S_PAINT.FRAGMENTS : sept compositions Lit/prepasse candidates passent (forward, environnement, SSAO, profondeur, normales, ombres, orthographique), dix textures et UV0 float4 conserves, 134 operations originales retenues, licence Bevy copiee. [Preuve](../townsmith-s-paint-lit-fragments-v1/RESULT.v1.json). Layout/pipeline et rendu GPU, fidelite HDRP, sampling implicite et Studio non qualifies ; aucun asset complet ajoute.\n')
    subprocess.run([sys.executable, '-B', str(S / 'record_batch_progress_v1.py')], check=True, stdout=subprocess.DEVNULL)
    manifest = json.loads((T / 'LOTS_ET_AVANCEMENT.v1.json').read_text(encoding='utf-8-sig'))
    for path, expected in manifest['input_pins'].items():
        assert sha(path) == expected, path
    print('Seven native lighting/prepass compositions recorded; dashboard pins verified.')


if __name__ == '__main__':
    main()
