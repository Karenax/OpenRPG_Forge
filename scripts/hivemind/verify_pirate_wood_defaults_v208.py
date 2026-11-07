"""Reopen four native shader defaults and reverse exactly the retained material diff."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,struct,re
W=Path('F:/OpenRPG_ImportWork');CAP=W/'pirate-material-native-defaults-v207';O=W/'pirate-wood-defaults-reopen-v208'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def f32(x):return struct.pack('<f',x)
assert not O.exists()
run=read(CAP/'RUN-RESULT.v1.json');assert run['status']=='pirate_native_shader_defaults_observed_inputs_unchanged' and run['unity_exit_code']==0 and not run['input_changes']
pins=read(CAP/'PREPARED.v1.json')['input_pins']|run['output_pins']
for p,h in pins.items():assert sha(p)==h,p
native=read(CAP/'MATERIAL-NATIVE.v1.json');assert native['source_materials_unchanged']
material=native['materials'][0];assert material['guid']=='abc00000000004592200770135964921'
props={p['name']:p for p in material['properties']}
guardpath=W/'pirate-source-thumbnail-batch-0121-0128-guard-v202/RESULT.v1.json';guard=read(guardpath);change=guard['changes'][0]
assert change['guid']==material['guid']
before=Path(change['prior_snapshot']);after=Path(change['path'])
assert sha(before)==change['before_sha256'] and sha(after)==change['after_sha256']
original=before.read_text('utf-8-sig');restored=after.read_text('utf-8-sig');rows=[]
for name,value in {'_DstBlend2':0,'_ExcludeFromTUAndAA':0,'_MaterialTypeMask':2,'_PerPixelSorting':0}.items():
    p=props[name];assert f32(p['value_float'])==f32(value)==f32(p['default_float']),name
    line=f'    - {name}: {value}\n';assert restored.count(line)==1
    restored=restored.replace(line,'');rows.append(dict(name=name,native_value=p['value_float'],shader_default=p['default_float'],equals_shader_default=True))
stencil=[]
for name,old,new in [('_StencilWriteMaskDepth',8,9),('_StencilWriteMaskGBuffer',14,15),('_StencilWriteMaskMV',40,41)]:
    line=f'    - {name}: {new}\n';assert restored.count(line)==1
    restored=restored.replace(line,f'    - {name}: {old}\n');stencil.append(dict(name=name,before=old,after=new))
assert restored==original,'Entire material diff must be explained'
for name in ['_StencilRefDepth','_StencilRefGBuffer','_StencilRefMV']:
    pattern=r'^\s*- '+name+r': (\d+)\s*$'
    values=[int(re.search(pattern,t,re.M)[1]) for t in [original,after.read_text('utf-8-sig')]]
    assert values[0]==values[1] and values[1]&1==0,name
for p in [Path(__file__),guardpath,CAP/'RUN-RESULT.v1.json',CAP/'PREPARED.v1.json',before,after]:pins[str(p)]=sha(p)
result=dict(status='four_wood_native_defaults_and_entire_material_reverse_diff_verified',
    native_float_properties=rows,stencil_changes=stencil,stencil_references_unchanged=True,
    textures_keywords_and_other_serialized_values_unchanged=True,material_guid=material['guid'],
    render_equivalence_qualified=False,full_import=False,input_pins=pins,finished_utc=datetime.now(timezone.utc).isoformat())
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps(dict(status=result['status'],default_properties=len(rows),stencil_changes=stencil)))
