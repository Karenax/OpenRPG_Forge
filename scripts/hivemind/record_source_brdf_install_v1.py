"""Record the implemented BRDF snapshot without promoting pending checks."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import re

R = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'source-brdf-lookup-install-v1'
ENGINE = R / 'crates/forge_engine/src'


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    originals = [ROOT / (name + '.before') for name in [
        'source_graph.rs', 'source_graph_program.rs', 'source_unlit_forward.rs',
        'source_graph_shadow_routing.rs', 'source_graph_frame.rs']]
    files = [ENGINE / name for name in ['source_graph.rs', 'source_graph_program.rs',
        'source_unlit_forward.rs', 'source_graph_shadow_routing.rs',
        'source_graph_preintegrated_brdf.rs', 'shaders/source_preintegrated_brdf.wgsl']]
    files += [R/'apps/forge_studio/src/authoring_assets/source_graph_frame.rs',
        Path(__file__).resolve(), W/'source-brdf-lookup-case-v1/CASE.v1.json',
        W/'firepit-brdf-lookup-gpu-case-v1/RESULT.v1.json']
    pattern = r'pub struct SourceGraphUniforms \{.*?\n\}'
    previous = re.search(pattern, originals[0].read_text('utf-8'), re.S).group(0)
    current = re.search(pattern, files[0].read_text('utf-8'), re.S).group(0)
    assert previous == current, 'Source basis/time ABI changed'
    data = dict(status='shared_source_brdf_implemented_checks_pending',
        recorded_utc=datetime.now(timezone.utc).isoformat(), sub_lot='L01.ECLAIRAGE.BRDF',
        original_basis_and_time_uniform_abi_preserved=True, binding=122,
        format='Rgba32Float', shared_image=True, missing_required_image_default=False,
        native_storage_tests_receipt=str(W/'source-brdf-storage-check-v2/RUN-RESULT.v1.json'),
        expected_storage_tests=4, expected_gpu_unique_cases=35,
        target_gpu_qualified=False, web_snapshot_qualified=False,
        render_fidelity_qualified=False, editor_visual_verified=False, full_import=False,
        preimage_pins={str(p):sha(p) for p in originals},
        input_pins={str(p):sha(p) for p in files},
        next_action='Await actual native test receipt, then explicitly run the prepared GPU witness and Web snapshot 18 one at a time')
    path = ROOT/'IMPLEMENTED.v1.json'
    if path.exists():
        old=json.loads(path.read_text('utf-8'))
        assert old['input_pins']==data['input_pins'], 'Snapshot already exists with different inputs'
    else:
        path.write_text(json.dumps(data,indent=2),'utf-8')
    print(json.dumps(dict(status=data['status'], receipt=str(path),
        source_uniform_abi_preserved=True, source_files_pinned=len(files))))


if __name__=='__main__':
    main()
