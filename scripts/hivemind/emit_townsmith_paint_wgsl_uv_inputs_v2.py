"""Regenerate arithmetic from original IR using declared implicit UV channels."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import subprocess
import sys

S=Path(__file__).parent
W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'townsmith-s-paint-wgsl-uv-inputs-v2'
assert not ROOT.exists()
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
old=W/'townsmith-s-paint-wgsl-v1/emit_typed_program.v1.py'
receipt=json.loads((old.parent/'RESULT.v1.json').read_text(encoding='utf-8'))
assert sha(old)==receipt['input_pins'][str(old)]
q=old.read_text(encoding='utf-8')
assert q.count('townsmith-s-paint-wgsl-v1')==1
q=q.replace('townsmith-s-paint-wgsl-v1','townsmith-s-paint-wgsl-uv-inputs-v2')
assert 'townsmith-s-paint-wgsl-v1' not in q
q="import sys\nsys.path.insert(0,"+repr(str(S))+ ")\nfrom source_graph_uv_inputs import implicit_uv_input, checked_channel, channel_expression\nrequired_uv_channels=set()\n"+q
a=" if 'ref' in v:return 'n'+v['ref'],width[v['ref']]"
assert q.count(a)==1
q=q.replace(a,a+"\n implicit=implicit_uv_input(v)\n if implicit is not None:\n  expr,w,channel=implicit;required_uv_channels.add(channel);return expr,w")
a=" if k=='UVNode':expr='source_uv';w=4"
assert q.count(a)==1
q=q.replace(a," if k=='UVNode':\n  channel=checked_channel(node['source_parameters']['m_OutputChannel']);required_uv_channels.add(channel);expr=channel_expression(channel);w=4")
a="fn evaluate_source_material(source_uv:vec4<f32>,source_absolute_position:"
assert q.count(a)==1
q=q.replace(a,"fn evaluate_source_material(source_uv:vec4<f32>,"+"'+','.join(channel_expression(c)+':vec4<f32>' for c in sorted(required_uv_channels) if c!=0)+(',' if required_uv_channels-{0} else '')+'"+"source_absolute_position:")
a="'translated_operations':len(lines),"
q=q.replace(a,"'required_original_uv_channels':sorted(required_uv_channels),"+a)
compile(q,'derived_emitter','exec')
ROOT.mkdir()
emitter=ROOT/'emit_typed_program.v2.py'
emitter.write_text(q,encoding='utf-8')
with (ROOT/'stdout.log').open('xb') as out,(ROOT/'stderr.log').open('xb') as err:
    child=subprocess.run([sys.executable,'-B',str(emitter)],stdout=out,stderr=err)
assert child.returncode==0,(ROOT/'stderr.log').read_text(encoding='utf-8')
program=ROOT/'typed-program-v1/material-program.wgsl'
generated=program.read_text(encoding='utf-8')
original=(old.parent/'typed-program-v1/material-program.wgsl').read_text(encoding='utf-8')
expected=original.replace('textureSample(t2,s2,vec2<f32>(0.0,0.0))','textureSample(t2,s2,source_uv1.xy)').replace('fn evaluate_source_material(source_uv:vec4<f32>,','fn evaluate_source_material(source_uv:vec4<f32>,source_uv1:vec4<f32>,')
assert generated==expected,'Regenerated arithmetic changed beyond the declared UV1 correction'
gen=json.loads((program.parent/'GENERATION.v1.json').read_text(encoding='utf-8'))
assert gen['required_original_uv_channels']==[0,1]
paths=[Path(__file__),S/'source_graph_uv_inputs.py',old,old.parent/'RESULT.v1.json',emitter,program,program.parent/'GENERATION.v1.json',W/'townsmith-s-paint-source-program-v1/material-family-active-ir-v1/abc00000000009486088228754689894.json']
result=dict(status='original_ir_reemitted_with_declared_implicit_uv1',finished_utc=datetime.now(timezone.utc).isoformat(),input_pins={str(p):sha(p) for p in paths},translated_operations=gen['translated_operations'],required_original_uv_channels=[0,1],all_other_generated_arithmetic_byte_equal=True,source_ir_unchanged=True,full_import=False,forge_editor_verified=False,complete_assets_delivered_increment=0)
(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
