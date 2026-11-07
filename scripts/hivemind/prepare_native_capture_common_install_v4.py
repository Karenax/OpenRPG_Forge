"""Install after the 103 scoped engine checks; keep the broad-suite refusal intact."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
base = S / 'queue_native_capture_common_install_v3.py'
target = S / 'queue_native_capture_common_install_v4.py'
assert not target.exists()
source = base.read_text(encoding='utf-8')
source = source.replace('native-texture-capture-common-install-v3', 'native-texture-capture-common-install-v4')
source = source.replace('source-graph-ten-texture-engine-check-v2', 'source-graph-ten-texture-engine-check-v3')
anchor = "('engine','forge_engine','source_')"
assert source.count(anchor) == 1
source = source.replace(anchor, "('engine','forge_engine','source_graph::'),('unlit','forge_engine','source_unlit_forward::'),('program','forge_engine','source_graph_program::'),('glb','forge_engine','source_graph_glb::')")
compile(source, str(target), 'exec')
target.write_text(source, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
