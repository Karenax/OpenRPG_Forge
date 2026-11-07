"""Preserve Unity DynamicValue scalar defaults; refuse unqualified matrix/vector cases."""
from pathlib import Path
import datetime, hashlib, json

W = Path('F:/OpenRPG_ImportWork')
I = W/'pirate-palm-wgsl-input-v542'
O = W/'pirate-palm-wgsl-v542'
def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f,'sha256').hexdigest()
def read(p): return json.loads(Path(p).read_text('utf-8-sig'))
assert not I.exists() and not O.exists()
previous = W/'pirate-palm-wgsl-input-v538'
batch = read(previous/'BATCH.v1.json')
for p,h in batch['source_pins'].items(): assert sha(p)==h,p
ir = read(batch['rows'][0]['output'])
dynamic = [(n,i) for n in ir['nodes'] for i in n['inputs']
           if i['slot_type'].endswith('.DynamicValueMaterialSlot') and 'literal' in i['value']]
assert len(dynamic)==1
n,i = dynamic[0]
assert n['opcode']=='MultiplyNode' and i['name']=='B'
assert i['value']['literal']==i['value']['source_slot']['m_Value']
sdk = W/'unity-library-source-thumbnail-pirate-bench-v2/project/Library/PackageCache/com.unity.shadergraph@8d7376ec43ee/Editor/Data/Graphs/DynamicValueMaterialSlot.cs'
sdk_text=sdk.read_text('utf-8-sig')
assert 'string values = NodeUtils.FloatToShaderValue(value.m00);' in sdk_text
assert 'if (channelCount == 1)' in sdk_text
native=W/'pirate-palm-native-contract-v535/source-generated.shader'
native_text=native.read_text('utf-8-sig')
assert 'Unity_Multiply_float_float(_Split_d87bbe0ad0344d108e0f2d2d1440294a_R_1_Float, 3, _Multiply_1cbfd565de4e49caa291ae060d779aa6_Out_2_Float);' in native_text
assert i['value']['literal']['e00']==3.0
emitter=(previous/'native-resource-emitter.py.txt').read_text('utf-8-sig')
needle=" if isinstance(x,dict):return vec(x[k] for k in ('x','y','z','w') if k in x)"
replacement=""" if kind=='DynamicValueMaterialSlot':
  assert isinstance(x,dict) and set(x)=={f'e{r}{c}' for r in range(4) for c in range(4)},'Malformed dynamic value'
  linked=[dims[q['value']['ref']] for q in node['inputs'] if 'ref' in q['value']]
  assert linked and max(linked)==1,'Dynamic vector/matrix literal requires separately qualified concrete type'
  return 'vec4<f32>('+scalar(x['e00'])+')'
 if isinstance(x,dict):
  assert any(k in x for k in ('x','y','z','w')),'Unknown dictionary literal; refusing silent zero'
  return vec(x[k] for k in ('x','y','z','w') if k in x)"""
assert emitter.count(needle)==1
emitter=emitter.replace(needle,replacement)
code=(previous/'configured_existing_generator.py').read_text('utf-8-sig').replace('v538','v542')
I.mkdir()
(I/'native-resource-emitter.py.txt').write_text(emitter,'utf-8')
(I/'configured_existing_generator.py').write_text(code,'utf-8')
for p in [Path(__file__),sdk,native,previous/'native-resource-emitter.py.txt',previous/'configured_existing_generator.py',I/'native-resource-emitter.py.txt',I/'configured_existing_generator.py']:
    batch['source_pins'][str(p)]=sha(p)
(I/'BATCH.v1.json').write_text(json.dumps(batch,indent=2),'utf-8')
exec(compile(code,str(I/'configured_existing_generator.py'),'exec'),{'__name__':'__main__','__file__':str(I/'configured_existing_generator.py')})
result=read(O/'RESULT.v1.json')
assert result['failed_materials']==0 and result['naga_validated_materials']==1
shader=Path(result['rows'][0]['wgsl']).read_text('utf-8-sig')
assert 'let n4=vec4<f32>(((n3)*(vec4<f32>(3.0))).x);' in shader
receipt={'status':'source_dynamic_scalar_restored_naga_pass_gpu_comparison_pending',
         'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
         'source_ir_unchanged':sha(batch['rows'][0]['output'])==batch['rows'][0]['output_sha256'],
         'source_scalar':3.0,'previous_emitted_scalar':0.0,
         'sdk_dynamic_scalar_rule_sha256':sha(sdk),'native_shader_sha256':sha(native),
         'shader_sha256':sha(result['rows'][0]['wgsl']),
         'gpu_parity':False,'full_import':False,'catalog_mutated':False,
         'limits':['Scalar defaults only; unresolved dynamic vector/matrix types refuse emission.',
                   'Naga validation does not prove numeric or rendered parity.']}
(O/'DYNAMIC-SCALAR-REPAIR.v1.json').write_text(json.dumps(receipt,indent=2),'utf-8')
print(json.dumps(receipt))
