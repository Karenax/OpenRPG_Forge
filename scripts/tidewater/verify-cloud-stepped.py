"""Check persistent cloud GPU history against batch execution and independent repeat."""
from pathlib import Path
import hashlib,json,sys
assert len(sys.argv)==5
batch,stepped,repeat,output=map(Path,sys.argv[1:]);assert not output.exists()
names=['weather.rgba8.bin','bounds.rgba8.bin','shadow.r32.bin','radiance.rgba16.bin','metadata.rgba16.bin','color0.rgba16.bin','meta0.rgba16.bin','color1.rgba16.bin','meta1.rgba16.bin','panorama.rgba16.bin']
for root in [stepped,repeat]:
 probe=json.loads((root/'probe.json').read_text());execution=json.loads((root/'execution.json').read_text())
 assert probe['stepped_render_graph'] and probe['native_controller'] and probe['native_atmosphere']
 assert not probe['source_cloud_frame_uniforms_uploaded'] and not probe['source_results_uploaded']
 assert execution['completed_steps']==execution['render_graph_submissions']==120
files={}
for name in names:
 a,b,c=[(root/name).read_bytes() for root in [batch,stepped,repeat]]
 assert a==b==c,name
 files[name]=hashlib.sha256(b).hexdigest()
report={'steps':120,'distinct_render_graph_submissions':120,'exact_batch_and_repeat_outputs':files,'limits':['fixed camera and lighting','120 step warmup, not unbounded interactive animation','global scene and atmosphere uniforms still acquired','no performance verdict']}
output.write_text(json.dumps(report,indent=2));print(json.dumps({'outputs_exact':len(files),'submissions':120,'report':str(output)}))
