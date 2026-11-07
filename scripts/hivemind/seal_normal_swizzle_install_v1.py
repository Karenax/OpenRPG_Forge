"""Reopen installed-source epoch and all scoped test receipts."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import re
import subprocess

S = Path(__file__).parent
W = Path('F:/OpenRPG_ImportWork')
INSTALL = W / 'normal-decode-swizzle-common-install-v1'
ROOT = W / 'normal-decode-swizzle-common-install-reopen-v1'
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')


def read(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))


def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()


assert not ROOT.exists()
run = read(INSTALL / 'RUN-RESULT.v1.json')
assert run['status'] == 'normal_swizzle_common_installed_workspace_tests_passed'
installed = read(INSTALL / 'INSTALLED.v1.json')
pins = {str(p): sha(p) for p in [Path(__file__), INSTALL / 'RUN-RESULT.v1.json', INSTALL / 'INSTALLED.v1.json']}
counts = {}
for label in ('assets', 'engine', 'unlit', 'program', 'glb'):
    assert run[label + '_exit_code'] == 0
    log = INSTALL / (label + '.stdout.log')
    matches = re.findall(r'test result: ok\. (\d+) passed; 0 failed; 0 ignored;', log.read_text(encoding='utf-8'))
    assert len(matches) == 1, label
    counts[label] = int(matches[0])
    for p in [log, INSTALL / (label + '.stderr.log'), INSTALL / (label + '.COMMAND.v1.json')]:
        pins[str(p)] = sha(p)
assert counts == {'assets': 29, 'engine': 93, 'unlit': 4, 'program': 0, 'glb': 6}, counts
for p, h in installed['installed_source_pins'].items():
    assert sha(p) == h
    pins[p] = h
for p, epoch in installed['previous_source_epoch'].items():
    assert sha(epoch['snapshot']) == epoch['sha256']
    pins[epoch['snapshot']] = epoch['sha256']
candidate = W / 'normal-decode-swizzle-candidate-v1/source_graph_bundle.candidate.rs'
source = candidate.read_text(encoding='utf-8').replace('"imported_storage.candidate.rs"', '"source_graph_bundle/imported_storage.rs"').replace('"scalar_parameters.rs"', '"source_graph_bundle/scalar_parameters.rs"')
formatted = subprocess.run([str(BIN / 'rustfmt.exe'), '--edition', '2024', '--config', 'skip_children=true'], input=source.encode('utf-8'), capture_output=True)
assert formatted.returncode == 0
assert Path(next(iter(installed['installed_source_pins']))).read_bytes() == formatted.stdout
pins[str(candidate)] = sha(candidate)
ROOT.mkdir()
result = {'status': 'normal_swizzle_common_install_and_scoped_tests_reopened',
          'finished_utc': datetime.now(timezone.utc).isoformat(), 'tests_passed': counts, 'engine_tests_passed': 103,
          'program_module_own_tests': 0, 'installed_source_equals_formatted_tested_candidate': True,
          'previous_source_epoch_snapshot_retained': True, 'normal_swizzle_fix_installed': True,
          'full_import': False, 'gpu_draw_executed': False, 'forge_editor_verified': False,
          'complete_assets_delivered_increment': 0, 'input_pins': pins}
(ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps({k: v for k, v in result.items() if k != 'input_pins'}))
