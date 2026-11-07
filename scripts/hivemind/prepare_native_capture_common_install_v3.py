"""Rebase the guarded install only onto a proved formatting-only source epoch."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
BASE = S / 'queue_native_capture_common_install_v1.py'
target = S / 'queue_native_capture_common_install_v3.py'
assert not target.exists()
source = BASE.read_text(encoding='utf-8')
source = source.replace('native-texture-capture-common-install-v1', 'native-texture-capture-common-install-v3')
source = source.replace('source-graph-ten-texture-engine-check-v1', 'source-graph-ten-texture-engine-check-v2')
source = source.replace("('engine','forge_engine','source_graph::ten_texture_binding_tests')", "('engine','forge_engine','source_')")
old = " assert job['status']=='ten_texture_engine_binding_tests_running' and live(job['cargo_pid'])"
new = """ assert (job['status']=='ten_texture_engine_binding_tests_running' and live(job['cargo_pid'])) or (job['status']=='ten_texture_engine_binding_tests_passed_inputs_unchanged' and job['actual_exit_code']==0)
 basispath=W/'native-capture-common-format-epoch-v1/RESULT.v1.json';basis=read(basispath)
 assert basis['status']=='common_source_change_is_canonical_formatting_only'
 for p,h in basis['input_pins'].items():assert sha(p)==h,p
"""
assert source.count(old) == 1
source = source.replace(old, new)
old = " for p,h in verified['input_pins'].items():assert sha(p)==h,p"
new = """ for p,h in verified['input_pins'].items():
  frozen=basis['historical_source_snapshots'].get(p)
  if frozen and sha(p)!=h:assert sha(frozen)==h,p
  else:assert sha(p)==h,p
"""
assert source.count(old) == 1
source = source.replace(old, new)
start = source.index(' expected={}\n')
end = source.index(' ROOT.mkdir();before=', start)
source = source[:start] + " expected=basis['expected_live_source_pins']\n for p,h in expected.items():assert sha(p)==h,p\n" + source[end:]
old = "pins[str(Path(__file__))]=sha(__file__)"
assert source.count(old) == 1
source = source.replace(old, old + ";pins[str(basispath)]=sha(basispath);pins[str(Path(__file__).with_name('queue_native_capture_common_install_v1.py'))]=sha(Path(__file__).with_name('queue_native_capture_common_install_v1.py'))")
old = "'historical_source_snapshots':snapshots,'upstream_cargo_pid'"
assert source.count(old) == 1
source = source.replace(old, "'historical_source_snapshots':basis['historical_source_snapshots'],'current_source_snapshots':snapshots,'upstream_cargo_pid'")
anchor = '  for path,data in payloads.items():\n'
assert source.count(anchor) == 1
source = source.replace(anchor, """  rustfmt=BIN/'rustfmt.exe'
  for path,data in list(payloads.items()):
   formatted=subprocess.run([str(rustfmt),'--edition','2024','--config','skip_children=true'],input=data,capture_output=True,creationflags=subprocess.CREATE_NO_WINDOW)
   assert formatted.returncode==0,(path,formatted.stderr.decode('utf-8',errors='replace'))
   payloads[path]=formatted.stdout
""" + anchor)
compile(source, str(target), 'exec')
target.write_text(source, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
