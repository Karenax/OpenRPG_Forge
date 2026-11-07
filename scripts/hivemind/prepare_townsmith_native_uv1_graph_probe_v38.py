"""Verify original graph intermediates after the source UV1 export correction."""
from pathlib import Path
import subprocess
import sys

S=Path(__file__).parent
builder=S/'build_townsmith_native_uv1_graph_probe_v38.py'
batch=S/'run_townsmith_native_uv1_graph_probe_v38.py'
assert not builder.exists() and not batch.exists()
q=(S/'build_townsmith_native_graph_probe_v33.py').read_text(encoding='utf-8')
q=q.replace('v33','v38')
a="original = w/'townsmith-paint-door-normal-roughness-carrier-v4/SM_Door-Single-B.normal-roughness-control.candidate.glb'"
assert q.count(a)==1
q=q.replace(a,"original = w/'townsmith-source-uv1-carrier-v36/SM_Door-Single-B.source-uv1-control.candidate.glb'")
q=q.replace('run_townsmith_native_graph_probe_v38','run_townsmith_native_uv1_graph_probe_v38')
q=q.replace('w/\'townsmith-door-source-uv-reopen-v25/RESULT.v1.json\'','w/\'townsmith-door-source-uv-reopen-v25/RESULT.v1.json\',w/\'townsmith-s-paint-wgsl-uv-inputs-v2/RESULT.v1.json\'')
q=q.replace('.read_text()',".read_text(encoding='utf-8')")
compile(q,str(builder),'exec')
builder.write_text(q,encoding='utf-8')
q=(S/'run_townsmith_native_graph_probe_v33.py').read_text(encoding='utf-8').replace('v33','v38')
compile(q,str(batch),'exec')
batch.write_text(q,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(builder)],check=True)
