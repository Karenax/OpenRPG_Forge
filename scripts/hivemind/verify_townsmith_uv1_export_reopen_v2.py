"""Reopen regenerated arithmetic and actual GPU carrier without trusting status."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import struct
import re
import subprocess
import sys

S=Path(__file__).parent
W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'townsmith-uv1-export-reopen-v2'
assert not ROOT.exists()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
pins={}
def reopen(p):
    value=read(p)
    for name,expected in value['input_pins'].items():
        assert sha(Path(name))==expected,name
        pins[name]=expected
    pins[str(p)]=sha(p)
    return value
generation=reopen(W/'townsmith-s-paint-wgsl-uv-inputs-v2/RESULT.v1.json')
assert generation['source_ir_unchanged'] and generation['required_original_uv_channels']==[0,1]
program=W/'townsmith-s-paint-wgsl-uv-inputs-v2/typed-program-v1/material-program.wgsl'
lines=lambda text:[line.strip() for line in text.splitlines() if re.match(r'let n\d+:',line.strip())]
expected=lines(program.read_text(encoding='utf-8'))
assert len(expected)==134
carrier=W/'townsmith-source-uv1-carrier-v36/SM_Door-Single-B.source-uv1-control.candidate.glb'
reopen(carrier.parent/'RESULT.v1.json')
b=carrier.read_bytes();n=struct.unpack_from('<I',b,12)[0];doc=json.loads(b[20:20+n]);binary=b[28+n:]
contract=doc['materials'][0]['extensions']['OPENRPG_source_graph']
stages=[]
for name in ['forward','prepass']:
    ref=contract[name];view=doc['bufferViews'][ref['buffer_view']]
    raw=binary[view.get('byteOffset',0):view.get('byteOffset',0)+view['byteLength']]
    assert hashlib.sha256(raw).hexdigest()==ref['sha256']
    text=raw.decode('utf-8')
    assert lines(text)==expected,name
    assert '@location(12) source_uv1:vec4<f32>' in text
    assert 'textureSample(t2,s2,source_uv1.xy)' in text
    stages.append(name)
uv_rows=[]
for mesh in doc['meshes']:
    for primitive in mesh['primitives']:
        a=doc['accessors'][primitive['attributes']['_OPENRPG_SOURCE_UV1']]
        assert a['componentType']==5126 and a['type']=='VEC4'
        view=doc['bufferViews'][a['bufferView']];stride=view.get('byteStride',16)
        offset=view.get('byteOffset',0)+a.get('byteOffset',0)
        values=[struct.unpack_from('<4f',binary,offset+i*stride) for i in range(a['count'])]
        assert any(x!=0 or y!=0 for x,y,z,w in values)
        uv_rows.append(dict(vertices=a['count'],accessor=primitive['attributes']['_OPENRPG_SOURCE_UV1'],original_four_components_preserved=True))
ROOT.mkdir()
with (ROOT/'tests.stdout.log').open('xb') as stdout,(ROOT/'tests.stderr.log').open('xb') as stderr:
    run=subprocess.run([sys.executable,'-B','-m','unittest','test_source_graph_uv_inputs'],cwd=S,stdout=stdout,stderr=stderr)
assert run.returncode==0
for p in [Path(__file__),S/'source_graph_uv_inputs.py',S/'test_source_graph_uv_inputs.py',carrier,program,ROOT/'tests.stdout.log',ROOT/'tests.stderr.log']:
    pins[str(p)]=sha(p)
result=dict(status='declared_uv_inputs_regenerated_and_reopened_in_both_gpu_stages',finished_utc=datetime.now(timezone.utc).isoformat(),input_pins=pins,rehashed_pins=len(pins),translated_operations=134,all_generated_operation_lines_match_gpu_carrier=True,verified_stages=stages,original_uv1_accessors=uv_rows,regression_tests_exit_code=run.returncode,full_import=False,forge_editor_verified=False,complete_assets_delivered_increment=0)
(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
