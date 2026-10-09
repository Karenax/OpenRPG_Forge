"""Install the three exact tested files while preserving concurrent changes."""
from pathlib import Path
import datetime
import json
import check_source_unlit_canonical_v1 as proof

R = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
ROOT = proof.W / 'source-unlit-forward-install-v1'

def save(data):
    temporary = ROOT / 'INSTALL.v1.json.tmp'
    temporary.write_text(json.dumps(data, indent=2), 'utf-8')
    temporary.replace(ROOT / 'INSTALL.v1.json')

def main():
    assert not ROOT.exists(), 'Do not overwrite installation evidence'
    receipt_path = proof.W / 'source-unlit-targeted-native-check-v2/RUN-RESULT.v1.json'
    receipt = proof.read(receipt_path)
    assert receipt['status'] == 'actual_engine_material_and_factory_tests_passed'
    assert receipt['actual_engine_tests_passed'] == 75
    assert receipt['actual_material_tests_passed'] == 4 and receipt['actual_program_tests_passed'] == 3
    prepared = proof.read(proof.ROOT / 'PREPARED.v1.json')
    proof.check(prepared['input_pins'])
    proof.check(prepared['frozen_pins'])
    prefix = ('#[path="source_unlit_forward.rs"]\nmod source_unlit_forward;\n'
        'pub use source_unlit_forward::{SourceUnlitForwardExtension,SourceUnlitForwardKey,SourceUnlitForwardMaterial,SourceUnlitForwardPlugin};\n'
        '#[path="source_unlit_forward_program.rs"]\nmod source_unlit_forward_program;\n'
        'pub use source_unlit_forward_program::{SourceUnlitForwardProgram,register_source_unlit_forward_material};\n')
    bridge_relative = 'crates/forge_engine/src/source_graph.rs'
    candidate = (proof.SNAP / bridge_relative).read_text('utf-8')
    assert candidate.startswith(prefix)
    plugin = '\n        app.add_plugins(SourceUnlitForwardPlugin);'
    assert candidate.count(plugin) == 1
    expected_bridge = candidate[len(prefix):].replace(plugin, '')
    target_bridge = R / bridge_relative
    assert target_bridge.read_text('utf-8') == expected_bridge, 'Concurrent bridge change'
    mask_install = proof.read(proof.W / 'source-graph-production-color-mask-install-v1/INSTALL.v1.json')
    row = next(item for item in mask_install['files'] if item['relative_path'] == bridge_relative)
    assert proof.sha(target_bridge) == row['installed_sha256'], 'Installed mask preimage changed'
    relatives = ['crates/forge_engine/src/source_unlit_forward.rs',
        'crates/forge_engine/src/source_unlit_forward_program.rs', bridge_relative]
    originals = {}
    for relative in relatives:
        target = R / relative
        if relative != bridge_relative:
            assert not target.exists(), 'Concurrent module creation'
        originals[relative] = proof.sha(target) if target.exists() else None
    ROOT.mkdir()
    state = dict(status='installing', proof_path=str(receipt_path), proof_sha256=proof.sha(receipt_path),
        files=[], full_import=False, current_product_build_verified=False,
        gpu_render_qualified=False, loader_admission_qualified=False)
    save(state)
    try:
        for relative in relatives:
            target = R / relative
            original = originals[relative]
            if original is not None:
                assert proof.sha(target) == original
                backup = ROOT / 'before' / relative
                backup.parent.mkdir(parents=True, exist_ok=True)
                backup.write_bytes(target.read_bytes())
                assert proof.sha(backup) == original
            else:
                assert not target.exists()
            source = proof.SNAP / relative
            expected = prepared['frozen_pins'][str(source)]
            assert proof.sha(source) == expected
            temporary = target.with_name(target.name + '.hivemind-forward-install.tmp')
            assert not temporary.exists()
            temporary.write_bytes(source.read_bytes())
            assert proof.sha(temporary) == expected
            assert proof.sha(target) == original if original is not None else not target.exists()
            temporary.replace(target)
            assert proof.sha(target) == expected
            state['files'].append(dict(relative_path=relative, original_sha256=original,
                installed_sha256=expected))
            save(state)
        state.update(status='tested_forward_modules_installed_product_build_pending', installed_files=3)
    except Exception as error:
        state.update(status='installation_refused_or_partial', error=repr(error))
        raise
    finally:
        state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        save(state)
    print(json.dumps(state))

if __name__ == '__main__':
    main()
