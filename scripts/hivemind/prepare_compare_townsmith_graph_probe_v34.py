"""Reopen the corrected source probe against the completed native v33 controls."""
from pathlib import Path
import subprocess,sys
s=Path(__file__).parent
target=s/'compare_townsmith_graph_probe_v34.py';assert not target.exists()
q=(s/'compare_townsmith_graph_probe_v33.py').read_text(encoding='utf-8')
q=q.replace('townsmith-door-source-graph-probe-v33','townsmith-door-source-graph-probe-v34')
q=q.replace('townsmith-graph-intermediate-comparison-v33','townsmith-graph-intermediate-comparison-v34')
compile(q,str(target),'exec');target.write_text(q,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(target)],check=True)
