"""Prove regression tests fail with the original first-component-only validator."""
from pathlib import Path
import json
import subprocess
import sys

S = Path(__file__).parent
W = Path('F:/OpenRPG_ImportWork')
source_root = W / 'normal-decode-swizzle-candidate-v1'
baseline_root = W / 'normal-decode-swizzle-baseline-candidate-v1'
assert not baseline_root.exists()
baseline_root.mkdir()
main = source_root / 'source_graph_bundle.candidate.rs'
candidate = main.read_text(encoding='utf-8')
original = (S.parents[1] / 'crates/forge_assets/src/source_graph_bundle.rs').read_text(encoding='utf-8')
start = candidate.index('                // A vector read consumes every selected profile')
end = candidate.index('\n            }\n        }\n        Ok(())', start)
old_start = original.index('                let channel = tail')
old_end = original.index('\n            }\n        }\n        Ok(())', old_start)
baseline = candidate[:start] + original[old_start:old_end] + candidate[end:]
for p in source_root.glob('*.rs'):
    (baseline_root / p.name).write_bytes(p.read_bytes())
(baseline_root / main.name).write_text(baseline, encoding='utf-8')
helper = (S / 'check_townsmith_material_packet_v1.py').read_text(encoding='utf-8')
helper = helper.replace('townsmith-s-paint-material-packet-check-v1', 'normal-decode-swizzle-baseline-check-v1').replace('normal-decode-swizzle-candidate-v1', 'normal-decode-swizzle-baseline-candidate-v1')
target = S / 'run_normal_swizzle_baseline_v1.py'
assert not target.exists()
target.write_text(helper, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
root = W / 'normal-decode-swizzle-baseline-check-v1'
result = json.loads((root / 'RESULT.v1.json').read_text(encoding='utf-8'))
out = (root / 'tests.stdout.log').read_text(encoding='utf-8')
assert result['compile_exit_code'] == 0 and result['test_exit_code'] != 0
assert '35 passed; 2 failed' in out, out[-2500:]
assert 'integrity_admission_requires_every_swizzled_normal_profile ... FAILED' in out
assert 'full_schema6_material_packet_validates_and_refuses_missing_normal_profiles ... FAILED' in out
print('Original validator: two expected regression failures; corrected validator: 37 tests pass.')
