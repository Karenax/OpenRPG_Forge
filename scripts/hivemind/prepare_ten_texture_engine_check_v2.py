"""Retry after the forward-only constructor fix; retain both failed v1 runs."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
engine_base = S / 'check_ten_texture_engine_v1.py'
source = engine_base.read_text(encoding='utf-8')
source = source.replace('source-graph-ten-texture-engine-check-v1', 'source-graph-ten-texture-engine-check-v2')
source = source.replace("'source_graph::ten_texture_binding_tests'", "'source_'")
anchor = "'crates/forge_engine/src/source_graph_glb.rs']"
assert source.count(anchor) == 1
source = source.replace(anchor, "'crates/forge_engine/src/source_graph_glb.rs','crates/forge_engine/src/source_unlit_forward.rs']")
engine = S / 'check_ten_texture_engine_v2.py'
assert not engine.exists()
compile(source, str(engine), 'exec')
engine.write_text(source, encoding='utf-8')
installer_base = S / 'queue_native_capture_common_install_v1.py'
source = installer_base.read_text(encoding='utf-8')
source = source.replace('native-texture-capture-common-install-v1', 'native-texture-capture-common-install-v2')
source = source.replace('source-graph-ten-texture-engine-check-v1', 'source-graph-ten-texture-engine-check-v2')
source = source.replace("('engine','forge_engine','source_graph::ten_texture_binding_tests')", "('engine','forge_engine','source_')")
installer = S / 'queue_native_capture_common_install_v2.py'
assert not installer.exists()
compile(source, str(installer), 'exec')
installer.write_text(source, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(engine)], check=True)
