"""Preserve v141 refusal; repeat its full real-source probe with corrected base."""
from pathlib import Path
import json,subprocess,sys
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'browser-source-glb-v151'
def main():
    assert not O.exists()
    old=S/'run_browser_source_glb_v141.py';target=S/'run_browser_source_glb_v151.py';assert not target.exists()
    source=old.read_text('utf-8-sig')
    replacements={
        "O = W / 'browser-source-glb-v141'":"O = W / 'browser-source-glb-v151'",
        "CARGO_BUILD_JOBS='1', CARGO_INCREMENTAL='0',":"CARGO_BUILD_JOBS='1', CARGO_INCREMENTAL='0', RUSTFLAGS='-Cdebuginfo=0',",
        'ProcessId=9952':'ProcessId=2920',
        "'run_medieval_torch_particle_behavior_v140.py'":"'run_torch_geometry_v150.py'",
        'False, 9952)':'False, 2920)',
        'waiting_pid=9952':'waiting_pid=2920',
        "'medieval-torch-native-particle-behavior-v140/RUN-RESULT.v1.json'":"'medieval-torch-native-geometry-v150/RUN-RESULT.v1.json'",
        "['two_isolated_complete_parent_native_particle_timelines_verified', 'native_particle_behavior_refusal_retained']":"['complete_native_torch_geometry_buffers_lod_and_collider_exported', 'native_torch_geometry_refusal_retained']",
    }
    for a,b in replacements.items():
        assert source.count(a)==1,(a,source.count(a));source=source.replace(a,b)
    target.write_text(source,'utf-8');O.mkdir()
    (O/'RETRY-CONTEXT.v1.json').write_text(json.dumps(dict(predecessor='F:/OpenRPG_ImportWork/browser-source-glb-v141/RUN-RESULT.v1.json',correction='SourceGraphPhaseMaterial retains standard phase/uniform/key but has no unused PBR samplers; all authored texture and sky bindings preserved.',same_actual_example='verify_browser_source_glb_v141',source_fidelity_qualified=False,full_import=False),indent=2),'utf-8')
    subprocess.run([sys.executable,'-B',str(target)],check=True)
if __name__=='__main__':main()
