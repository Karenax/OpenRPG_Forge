"""Reopen native defaults without declaring the material migration equivalent."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, struct

W=Path('F:/OpenRPG_ImportWork')
CAP=W/'pirate-material-native-defaults-v186'
O=W/'pirate-material-native-defaults-reopen-v187'
def read(p): return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f: return hashlib.file_digest(f,'sha256').hexdigest()
def f32(v): return struct.pack('<f',v)

assert not O.exists(), 'Preserve prior proof'
run=read(CAP/'RUN-RESULT.v1.json')
assert run['status']=='pirate_native_shader_defaults_observed_inputs_unchanged'
assert run['unity_exit_code']==0 and not run['input_changes']
pins=read(CAP/'PREPARED.v1.json')['input_pins']|run['output_pins']
for p,h in pins.items(): assert sha(p)==h,p
native=read(CAP/'MATERIAL-NATIVE.v1.json')
mat=native['materials'][0]
assert mat['guid']=='a441b81a4efaff04a85744086c47c402' and mat['source_material_unchanged']
props={p['name']:p for p in mat['properties']}
assert len(props)==len(mat['properties'])
added={'_Contrast':1,'_DstBlend2':0,'_ExcludeFromTUAndAA':0,'_MaskPower':.5,
       '_MaskSize':2048,'_MaterialID':1,'_MaterialTypeMask':2,'_PerPixelSorting':0,
       '_TransmissionEnable':1,'_UseAOMap':1}
rows=[]
for name,saved in added.items():
    p=props[name]
    assert f32(p['value_float'])==f32(saved),name
    rows.append(dict(name=name,saved_value=saved,native_value=p['value_float'],
        shader_default=p['default_float'],equals_shader_default=f32(saved)==f32(p['default_float'])))
for name in ['_UseMask','_UseOpacity']:
    assert f32(props[name]['value_float'])==f32(0),name
graph_path=Path('F:/OpenRPG_ImportWork/unity-library-source-thumbnail-pirate-bench-v2/project')/native['graph_path']
text=graph_path.read_text('utf-8-sig');decoder=json.JSONDecoder();pos=0;objects=[]
while pos<len(text):
    while pos<len(text) and text[pos].isspace():pos+=1
    if pos==len(text):break
    obj,end=decoder.raw_decode(text,pos);objects.append(obj);pos=end
authored={o.get('m_OverrideReferenceName') or o.get('m_DefaultReferenceName'):o for o in objects
          if 'ShaderProperty' in o.get('m_Type','')}
authored_rows=[]
for name in ['_Contrast','_MaskPower','_MaskSize','_UseAOMap']:
    value=authored[name]['m_Value'];assert f32(value)==f32(added[name]),name
    authored_rows.append(dict(name=name,source_graph_default=value,equals_added_value=True))
mask=authored['_GrayScaleMask']
result=dict(status='native_added_values_and_authored_graph_defaults_independently_reopened',
    native_float_properties=rows,authored_graph_properties=authored_rows,
    native_mask_property=props['_GrayScaleMask'],authored_mask_property=mask,
    inactive_selectors={n:props[n]['value_float'] for n in ['_UseMask','_UseOpacity']},
    unresolved=['Prove the authored mask default is unused in every active source output.',
                'Classify depth, GBuffer and motion-vector stencil additions against the actual HDRP material recipe.'],
    source_equivalence_qualified=False,images_admitted=0,full_import=False,studio_verified=False,
    input_pins=pins,finished_utc=datetime.now(timezone.utc).isoformat())
for p in [Path(__file__),CAP/'RUN-RESULT.v1.json',CAP/'PREPARED.v1.json']:
    result['input_pins'][str(p)]=sha(p)
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps(dict(status=result['status'],added_defaults_equal=sum(r['equals_shader_default'] for r in rows),
    total_added=len(rows),authored_graph_defaults_equal=len(authored_rows),images_admitted=0)))
