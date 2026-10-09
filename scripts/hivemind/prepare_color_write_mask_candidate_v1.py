"""Stage a generic source-pass mask adapter without claiming renderer admission."""
from pathlib import Path
import datetime
import hashlib
import json
import shutil

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'source-graph-color-write-mask-candidate-v1'
REPO = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def main():
    assert not ROOT.exists()
    native = W / 'delta-firepit-native-tint-gpu-v3/RESULT.v1.json'
    proof = json.loads(native.read_text('utf-8-sig'))
    assert proof['destination_alpha_preserved'] and proof['unique_uniform_tint_hypothesis_qualified']
    candidate = Path(__file__).with_name('candidates') / 'source_graph_color_write_mask.rs'
    original = REPO / 'crates/forge_engine/src/source_graph_render_state.rs'
    pins = {str(p): sha(p) for p in [native, candidate, original, Path(__file__).resolve()]}
    ROOT.mkdir()
    for p in (candidate, original):
        destination = ROOT / p.name
        shutil.copyfile(p, destination)
        assert sha(destination) == pins[str(p)]
    for p, digest in pins.items():
        assert sha(Path(p)) == digest, 'changed candidate input: ' + p
    result = dict(status='generic_color_write_mask_candidate_staged', input_pins=pins,
        output_pins={str(ROOT / p.name): sha(ROOT / p.name) for p in (candidate, original)},
        candidate_pass_state_hash_includes_mask=True, rejects_unknown_mask_before_state_mutation=True,
        secondary_color_attachments_preserved=True, native_need='original legacy shader preserves destination alpha',
        scope='candidate code only; compilation, tests, GPU parity and actual material specialization registration pending',
        canonical_code_installed=False, compiled=False, tests_executed=False, gpu_parity_qualified=False,
        renderer_admission=False, full_import=False,
        created_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT / 'PREPARED.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps(dict(status=result['status'], compiled=False, renderer_admission=False)))

if __name__ == '__main__':
    main()
