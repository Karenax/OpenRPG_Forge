from pathlib import Path
import subprocess,sys
S=Path(__file__).parent
source=(S/'check_ten_texture_contract_v1.py').read_text(encoding='utf-8')
source=source.replace('source-graph-ten-texture-contract-check-v1','source-graph-ten-texture-engine-check-v1')
source=source.replace("'forge_assets','--lib','source_graph_bundle'","'forge_engine','--lib','source_graph::ten_texture_binding_tests'")
source=source.replace('ten_texture_wire_contract_tests','ten_texture_engine_binding_tests')
source=source.replace('engine_binding_tests_verified=False','engine_binding_tests_verified=True')
p=S/'check_ten_texture_engine_v1.py';assert not p.exists();p.write_text(source,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(p)],check=True)
