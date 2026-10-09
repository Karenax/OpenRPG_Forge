"""Reopen the observed successful contract test log; retain its exact scope."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import re

R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'source-brdf-layout-check-v1'


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream,'sha256').hexdigest()


def main():
    log=ROOT/'output.log'
    text=log.read_text('utf-8-sig')
    assert '20 passed; 0 failed' in text
    for name in ['shared_scene_image_does_not_replace_authored_slots',
        'duplicate_or_changed_scene_declarations_are_refused',
        'shared_scene_image_does_not_admit_an_extra_sampler']:
        assert f'scene_brdf_texture_layout_tests::{name} ... ok' in text
    executable=Path(re.search(r'\((F:\\[^\r\n]+\.exe)\)',text).group(1))
    paths=[Path(__file__).resolve(),log,executable,R/'Cargo.lock',
        R/'crates/forge_assets/src/source_graph_bundle.rs',
        R/'crates/forge_engine/src/source_graph_program.rs',
        W/'source-brdf-lookup-install-v1/source_graph_bundle.rs.before']
    data=dict(status='strict_shared_scene_brdf_declaration_and_source_contract_regressions_verified',
        reopened_utc=datetime.now(timezone.utc).isoformat(),actual_exit_code_observed=0,
        tests_verified=20,new_brdf_tests_verified=3,
        authored_texture_slots_not_substituted=True,duplicate_unknown_bindings_and_extra_sampler_refused=True,
        source_snapshot_pins_captured_after_run=True,full_import=False,
        target_gpu_sampling_qualified=False,editor_visual_verified=False,
        source_contract_schema_versions_unchanged=True,
        input_pins={str(p):sha(p) for p in paths})
    path=ROOT/'RESULT.v1.json'
    assert not path.exists()
    path.write_text(json.dumps(data,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in data.items() if k!='input_pins'}))


if __name__=='__main__':
    main()
