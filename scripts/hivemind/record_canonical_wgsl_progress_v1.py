"""Record a limited shader-composition result in the batch dashboard."""
from pathlib import Path
import hashlib
import json
import subprocess
import sys

S = Path(__file__).parent
W = Path('F:/OpenRPG_ImportWork')
T = W / 'SUIVI_LOTS_HIVEMIND'


def sha(path):
    with Path(path).open('rb') as handle:
        return hashlib.file_digest(handle, 'sha256').hexdigest()


def main():
    proof = W / 'townsmith-s-paint-canonical-wgsl-check-v1/RESULT.v1.json'
    result = json.loads(proof.read_text(encoding='utf-8'))
    assert result['status'] == 'canonical_arithmetic_wgsl_composition_passed'
    assert len(result['cases']) == 2 and all(c['actual_exit_code'] == 0 for c in result['cases'])
    for path, expected in result['input_pins'].items():
        assert sha(path) == expected, path
    marker = 'canonical_wgsl_close'
    path = S / 'record_batch_progress_v1.py'
    source = path.read_text(encoding='utf-8')
    assert marker not in source, 'Already recorded; preserve the existing journal.'
    anchor = " active_lot=data.get('current_work_sub_lot',active_lot);active_state=data.get('current_work_state',active_state)"
    assert source.count(anchor) == 1
    block = " canonical_wgsl_close=read(W/'townsmith-s-paint-canonical-wgsl-check-v1/RESULT.v1.json')\n if canonical_wgsl_close.get('status')=='canonical_arithmetic_wgsl_composition_passed':\n  data['current_work_state']+=' ; composition arithmetique WGSL validee pour deux groupes de bindings (sonde non executee)'\n  data['canonical_wgsl_proof']=str(W/'townsmith-s-paint-canonical-wgsl-check-v1/RESULT.v1.json')\n  key+=':'+canonical_wgsl_close['status']\n"
    updated = source.replace(anchor, block + anchor)
    compile(updated, str(path), 'exec')
    path.write_text(updated, encoding='utf-8')
    path = S / 'render_lot_dashboard_v1.py'
    source = path.read_text(encoding='utf-8')
    anchor = "    path = T / 'LOTS_ET_AVANCEMENT.md'"
    assert source.count(anchor) == 1
    block = "    canonical_wgsl_proof_path=W/'townsmith-s-paint-canonical-wgsl-check-v1/RESULT.v1.json'\n    output += '**Composition du shader arithmetique :** deux controles Bevy/Naga passent avec les bindings canoniques. Sonde de types composee, non executee ; passes completes et rendu Studio restent ouverts. [Preuve](../townsmith-s-paint-canonical-wgsl-check-v1/RESULT.v1.json). Aucun asset complet ajoute.\\n\\n'\n"
    source = source.replace(anchor, block + anchor)
    anchor = "    meta = T / 'LOTS_ET_AVANCEMENT.v1.json'"
    assert source.count(anchor) == 1
    source = source.replace(anchor, "    manifest['input_pins'][str(canonical_wgsl_proof_path)]=hashlib.sha256(canonical_wgsl_proof_path.read_bytes()).hexdigest()\n" + anchor)
    compile(source, str(path), 'exec')
    path.write_text(source, encoding='utf-8')
    with (T / 'JOURNAL.md').open('a', encoding='utf-8') as handle:
        handle.write('\n- ' + result['finished_utc'] + ' — L03.TOWNSMITH.SHADER.S_PAINT.COMPOSITION : deux controles Bevy/Naga du shader arithmetique avec bindings canoniques passent. Sonde non executee, rendu complet et Studio restants. [Preuve](../townsmith-s-paint-canonical-wgsl-check-v1/RESULT.v1.json). Aucun asset complet ajoute.\n')
    subprocess.run([sys.executable, '-B', str(S / 'record_batch_progress_v1.py')], check=True, stdout=subprocess.DEVNULL)
    manifest = json.loads((T / 'LOTS_ET_AVANCEMENT.v1.json').read_text(encoding='utf-8-sig'))
    for path, expected in manifest['input_pins'].items():
        assert sha(path) == expected, path
    print('Shader composition recorded; dashboard evidence hashes verified; zero complete assets added.')


if __name__ == '__main__':
    main()
