"""Prove the common GLB loader supplies its declared decoder, not fidelity."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json

W = Path('F:/OpenRPG_ImportWork')
OLD = W / 'townsmith-native-lighting-layer-0-v53'
NEW = W / 'townsmith-native-lighting-layer-0-v54'
ROOT = W / 'deferred-srgb-product-loader-reopen-v54'


def read(p):
    return json.loads(Path(p).read_text(encoding='utf-8-sig'))


def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()


assert not ROOT.exists()
pins = {}
historical = {row['original']: row for row in read(
    W / 'deferred-srgb-source-epoch-v53/SOURCE-EPOCH.v1.json')['files']}
resolved = []
for bench in [OLD, NEW]:
    run = read(bench / 'RUN-RESULT.v1.json')
    native = read(bench / 'NATIVE-RESULT.v1.json')
    assert run['actual_exit_code'] == 0 and run['gpu_draw_executed']
    assert native['gpu_draw_executed'] and native['consecutive_equal_readbacks'] >= 3
    assert sha(bench / 'NATIVE-RESULT.v1.json') == run['native_receipt_sha256']
    assert sha(bench / 'FORGE-LINEAR.rgba16f') == run['forge_image_sha256']
    for name, expected in read(bench / 'PREPARED.v1.json')['input_pins'].items():
        p = Path(name)
        if sha(p) != expected:
            assert bench == OLD and name in historical, name
            row = historical[name]
            p = Path(row['snapshot'])
            assert row['sha256'] == expected and sha(p) == expected
            resolved.append(dict(original=name, snapshot=str(p), sha256=expected))
        pins[str(p)] = expected
    for name in ['RUN-RESULT.v1.json', 'NATIVE-RESULT.v1.json', 'PREPARED.v1.json', 'FORGE-LINEAR.rgba16f']:
        p = bench / name
        pins[str(p)] = sha(p)
old_input, new_input = read(OLD / 'INPUT.v1.json'), read(NEW / 'INPUT.v1.json')
assert old_input['glb_sha256'] == new_input['glb_sha256']
assert (OLD / 'FORGE-LINEAR.rgba16f').read_bytes() == (NEW / 'FORGE-LINEAR.rgba16f').read_bytes()
example = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust/crates/forge_engine/examples/verify_source_glb_lighting_components_v54.rs')
text = example.read_text(encoding='utf-8')
assert 'install_deferred_srgb_lookup' not in text
assert '.register_captured_images(' in text
assert '.validate_scene_dependencies(&images)' in text
ROOT.mkdir()
result = dict(status='common_glb_loader_decoder_installation_gpu_verified',
    finished_utc=datetime.now(timezone.utc).isoformat(), lot='L03.TOWNSMITH.MATERIAUX.DEFERRED.CHARGEUR',
    fixture_decoder_installation_removed=True, actual_common_glb_loader_used=True,
    original_glb_unchanged=True, three_stable_gpu_readbacks=True, output_byte_equal_to_explicit_install=True,
    historical_source_epochs_resolved=resolved, input_pins=pins,
    full_material_fidelity_qualified=False, dimension_refusal_test_pending=True,
    actual_editor_visual_verified=False, full_import=False, complete_assets_delivered_increment=0)
(ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps({k: v for k, v in result.items() if k not in ['input_pins', 'historical_source_epochs_resolved']}))
