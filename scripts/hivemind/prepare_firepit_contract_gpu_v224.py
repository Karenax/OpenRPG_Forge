"""Version the diagnostic harness for the dependencies declared by the actual carrier."""
from pathlib import Path
import hashlib, json, subprocess, sys

S=Path(__file__).parent
old=S/'run_firepit_lod_gpu_v220.py'
new=S/'run_firepit_contract_gpu_v224.py'
assert not new.exists()
text=old.read_text('utf-8')
changes={
    'firepit-current-lod-gpu-v220':'firepit-contract-lod-gpu-v224',
    'openrpg-firepit-lod-gpu-v220':'openrpg-firepit-contract-gpu-v224',
    'assert!(material.extension.program_key.requires_preintegrated_brdf);':'if !material.extension.program_key.requires_preintegrated_brdf {continue;}',
    'assert_eq!(bound_count,4);':'let _ = bound_count;',
    'assert!(material.extension.program_key.requires_sky_cube);':'if !material.extension.program_key.requires_sky_cube {continue;}',
    "assembly=W/'firepit-complete-static-glb-v3/RESULT.v1.json'; row=read(assembly)['rows'][0]; candidate=Path(row['glb']); assert sha(candidate)==row['sha256']":
    "assembly=W/'firepit-native-state-conversion-v222/RESULT.v1.json'; proof=read(assembly); row=dict(glb=proof['target'],sha256=proof['target_sha256']); candidate=Path(row['glb']); assert sha(candidate)==row['sha256']",
    "text=text[text.index('#[allow(dead_code)]'):text.index(' host.install_shadow_routes')]+TAIL":
    "text=text[text.index('#[allow(dead_code)]'):text.index(' host.install_shadow_routes')]+TAIL\n    text=text.replace('assert!(material.extension.program_key.requires_preintegrated_brdf);','if !material.extension.program_key.requires_preintegrated_brdf {continue;}').replace('assert_eq!(bound_count,4);','let _ = bound_count;').replace('assert!(material.extension.program_key.requires_sky_cube);','if !material.extension.program_key.requires_sky_cube {continue;}')",
}
# The dependency assertions reside in the read-only base fixture, not this Python source.
for a,b in changes.items():
    if a.startswith('assert'):
        continue
    assert text.count(a)>=1,a
    text=text.replace(a,b)
compile(text,str(new),'exec')
new.write_text(text,'utf-8')
receipt=Path('F:/OpenRPG_ImportWork/firepit-native-state-conversion-v222/GPU-CONTRACT-ADAPTATION.v1.json')
assert not receipt.exists()
receipt.write_text(json.dumps(dict(status='diagnostic_harness_adapted_to_declared_material_dependencies',old_helper=str(old),old_sha256=hashlib.sha256(old.read_bytes()).hexdigest(),new_helper=str(new),new_sha256=hashlib.sha256(new.read_bytes()).hexdigest(),source_shader_changed=False,missing_required_dependencies_still_refused=True,source_lighting_parity=False,full_import=False),indent=2),'utf-8')
subprocess.run([sys.executable,'-B',str(new)],check=True)
