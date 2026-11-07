"""Preserve failed builder bytes; scope the new field to MaterialValues only."""
from pathlib import Path
import hashlib,json,subprocess,sys
s=Path(__file__).parent
builder=s/'build_townsmith_native_graph_probe_v33.py'
raw=builder.read_bytes();q=raw.decode('utf-8')
assert q.count("'alpha:f32,'")==2
q=q.replace("'alpha:f32,'","'    alpha:f32,'")
archive=Path('F:/OpenRPG_ImportWork/townsmith-native-graph-probe-builder-refusal-v33')
assert not archive.exists();archive.mkdir()
(archive/'failed-builder.py.saved').write_bytes(raw)
(archive/'RESULT.v1.json').write_text(json.dumps(dict(status='builder_ambiguous_alpha_anchor_refused',sha256=hashlib.sha256(raw).hexdigest(),original_sources_unchanged=True,full_import=False),indent=2),encoding='utf-8')
compile(q,str(builder),'exec');builder.write_text(q,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(builder)],check=True)
