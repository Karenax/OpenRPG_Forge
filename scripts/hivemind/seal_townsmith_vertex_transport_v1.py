"""Reopen vertex transport with all real composed module contents pinned."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import subprocess
import sys

S = Path(__file__).parent
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'townsmith-s-paint-vertex-transport-reopen-v1'
T = W / 'SUIVI_LOTS_HIVEMIND'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    assert not ROOT.exists()
    original = W / 'townsmith-s-paint-vertex-transport-v1/RESULT.v1.json'
    d = json.loads(original.read_text(encoding='utf-8'))
    assert d['status'] == 'original_static_vertex_and_uv0_transport_composed'
    pins = dict(d['input_pins'])
    toolroot = W / 'native-hdrp-nine-source-programs-v1/source-graph-real-lighting-composer-v2'
    prior = json.loads((toolroot / 'RESULT.v1.json').read_text(encoding='utf-8'))
    pins.update({r['path']: r['sha256'] for r in prior['primary_modules']})
    pins.update({str(p): sha(p) for p in (original, toolroot / 'RESULT.v1.json', Path(__file__))})
    for path, expected in pins.items():
        assert sha(path) == expected, path
    shader = Path(d['vertex_shader']).read_text(encoding='utf-8')
    # One input attribute and one varying in each of the two output structs.
    assert shader.count('@location(10) source_uv0:vec4<f32>') == 3
    assert 'struct SourceGraphForwardOutput {' in shader and 'struct SourceGraphPrepassOutput {' in shader
    assert 'out.source_uv0=vertex.source_uv0;' in shader and 'out.source_uv0=frame.source_uv0;' in shader
    probe = Path(d['diagnostic_probe']).read_text(encoding='utf-8')
    assert 'SourceMaterialHostInputs(input.source_uv0,' in probe
    ROOT.mkdir()
    cases = []
    for case in d['cases']:
        process = subprocess.run([str(toolroot / 'validate_real_bevy_closure.exe'), str(toolroot / 'module-map.tsv'), d['diagnostic_probe'], *case['shader_defines']], capture_output=True, creationflags=subprocess.CREATE_NO_WINDOW)
        label = case['permutation']
        out, err = ROOT / (label + '.stdout.log'), ROOT / (label + '.stderr.log')
        out.write_bytes(process.stdout)
        err.write_bytes(process.stderr)
        pins.update({str(p): sha(p) for p in (out, err)})
        cases.append({**case, 'reopen_exit_code': process.returncode})
    for path, expected in pins.items():
        assert sha(path) == expected, path
    success = all(c['reopen_exit_code'] == 0 for c in cases)
    result = {'status': 'four_vertex_uv0_transport_compositions_reopened' if success else 'vertex_transport_reopen_refusal_retained',
              'finished_utc': datetime.now(timezone.utc).isoformat(), 'cases': cases, 'input_pins': pins,
              'compositions_passed': sum(c['reopen_exit_code'] == 0 for c in cases),
              'uv0_components_preserved': 4, 'actual_gpu_execution_qualified': False,
              'lighting_fragment_created': False, 'renderer_installed': False, 'forge_editor_verified': False,
              'full_import': False, 'complete_assets_delivered_increment': 0}
    proof = ROOT / 'RESULT.v1.json'
    proof.write_text(json.dumps(result, indent=2), encoding='utf-8')
    assert success, 'Retain the failure; do not promote the dashboard.'
    path = S / 'record_batch_progress_v1.py'
    source = path.read_text(encoding='utf-8')
    assert 'vertex_transport_close' not in source
    anchor = " active_lot=data.get('current_work_sub_lot',active_lot);active_state=data.get('current_work_state',active_state)"
    assert source.count(anchor) == 1
    block = " vertex_transport_close=read(W/'townsmith-s-paint-vertex-transport-reopen-v1/RESULT.v1.json')\n if vertex_transport_close.get('compositions_passed')==4:\n  data['current_work_state']=data['current_work_state'].replace('transport UV0 complet, passes de rendu, sampling implicite et Studio restants','quatre variantes vertex/prepasse composees et reouvertes, UV0 float4 conserves ; fragments eclaires, execution GPU, sampling implicite et Studio restants')\n  data['vertex_transport_proof']=str(W/'townsmith-s-paint-vertex-transport-reopen-v1/RESULT.v1.json')\n  key+=':'+vertex_transport_close['status']\n"
    source = source.replace(anchor, block + anchor)
    compile(source, str(path), 'exec')
    path.write_text(source, encoding='utf-8')
    path = S / 'render_lot_dashboard_v1.py'
    source = path.read_text(encoding='utf-8')
    anchor = "    path = T / 'LOTS_ET_AVANCEMENT.md'"
    assert source.count(anchor) == 1
    block = "    vertex_transport_proof_path=W/'townsmith-s-paint-vertex-transport-reopen-v1/RESULT.v1.json'\n    output += '**Transport vertex/UV0 :** quatre compositions reouvertes (forward, profondeur, normales, orthographique) avec les modules Bevy epingles. UV0 float4 et index instance conserves. Normale tangentielle implicite derivee du shader genere Unity. Execution GPU et fragments de rendu eclaires restants. [Preuve](../townsmith-s-paint-vertex-transport-reopen-v1/RESULT.v1.json). Aucun asset complet ajoute.\\n\\n'\n"
    source = source.replace(anchor, block + anchor)
    anchor = "    meta = T / 'LOTS_ET_AVANCEMENT.v1.json'"
    assert source.count(anchor) == 1
    source = source.replace(anchor, "    manifest['input_pins'][str(vertex_transport_proof_path)]=hashlib.sha256(vertex_transport_proof_path.read_bytes()).hexdigest()\n" + anchor)
    compile(source, str(path), 'exec')
    path.write_text(source, encoding='utf-8')
    with (T / 'JOURNAL.md').open('a', encoding='utf-8') as handle:
        handle.write('\n- ' + result['finished_utc'] + ' — L03.TOWNSMITH.SHADER.S_PAINT.VERTEX : quatre variantes composees et reouvertes, UV0 float4 transportes sans troncature, index instance explicite ; tous modules Bevy epingles. Normale tangentielle implicite verifiee dans le shader genere Unity. Garde de reprise precedente refusee apres mise en forme concurrente ; tests moteur v2 relances sur empreintes actuelles, aucun changement concurrent ecrase. [Preuve](../townsmith-s-paint-vertex-transport-reopen-v1/RESULT.v1.json). Fragments eclaires et GPU/Studio restants ; aucun asset complet ajoute.\n')
    subprocess.run([sys.executable, '-B', str(S / 'record_batch_progress_v1.py')], check=True, stdout=subprocess.DEVNULL)
    print(json.dumps({k: v for k, v in result.items() if k != 'input_pins'}))


if __name__ == '__main__':
    main()
