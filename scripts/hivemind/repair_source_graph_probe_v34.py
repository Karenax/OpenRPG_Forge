"""Add the original SDK colour include to a new private probe version."""
from pathlib import Path
import subprocess,sys
s=Path(__file__).parent
target=s/'queue_townsmith_source_graph_probe_v34.py';assert not target.exists()
q=(s/'queue_townsmith_source_graph_probe_v33.py').read_text(encoding='utf-8')
q=q.replace('v33','v34').replace('V33','V34')
anchor='#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"'
assert q.count(anchor)==1
q=q.replace(anchor,anchor+'\\n#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"')
compile(q,str(target),'exec');target.write_text(q,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(target)],check=True)
