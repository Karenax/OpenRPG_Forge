"""Reopen authored palm bindings and actual generated pass state, without render claims."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, importlib.util, json, re, struct
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork')
B=W/'pirate-palm-native-contract-v528';O=W/'pirate-palm-render-contract-v529'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def bits(x):return struct.pack('<f',float(x))
spec=importlib.util.spec_from_file_location('source_contract_reader',S/'read_delta_foliage_render_contracts_v1.py')
reader=importlib.util.module_from_spec(spec);spec.loader.exec_module(reader)
def material(p):
    text=re.sub(r'^%.*\n','',Path(p).read_text('utf-8-sig'),flags=re.M)
    text=re.sub(r'^--- !u!\d+ &-?\d+.*$','---',text,flags=re.M)
    rows=[d['Material'] for d in reader.yaml.safe_load_all(text) if isinstance(d,dict) and 'Material' in d]
    assert len(rows)==1;return rows[0]
def blocks(text):
    for m in re.finditer(r'\bPass\s*\{',text):
        opening=text.index('{',m.start());depth=1
        for i in range(opening+1,len(text)):
            depth+=(text[i]=='{')-(text[i]=='}')
            if depth==0:yield text[opening+1:i];break
        else:raise ValueError('Unclosed generated pass')
assert not O.exists()
prep=read(B/'PREPARED.v1.json');run=read(B/'RUN-RESULT.v1.json');native=read(B/'NATIVE-RESULT.v1.json')
assert run['status']=='palm_native_shader_generated_contract_review_pending' and run['unity_exit_code']==0 and run['source_inputs_unchanged']
assert not run['source_changes'] and not run['private_importer_changes']
assert native['source_materials_unchanged'] and len(native['materials'])==1
pins=dict(prep['input_pins'])
for p,h in pins.items():assert sha(p)==h,p
original_path=Path(prep['original_material']);graph_path=Path(prep['original_graph'])
source=material(original_path)
private_path=next(Path(r['copy']) for r in prep['private_copies'] if r['source']==str(original_path))
private=material(private_path)
assert source['m_Shader']==private['m_Shader']
m=native['materials'][0];assert m['guid']=='abc00000000004284671107665666980' and m['file_id']=='2100000'
props={p['name']:p for p in m['properties']};assert len(props)==len(m['properties'])
authored=source['m_SavedProperties'];current=private['m_SavedProperties']
floats=reader.property_table(authored['m_Floats']);colors=reader.property_table(authored['m_Colors']);textures=reader.property_table(authored['m_TexEnvs'])
checks=[];inactive=[];migration_changes=[]
expected_stencil_changes={'_StencilWriteMaskDepth':(8,9),'_StencilWriteMaskGBuffer':(14,15),'_StencilWriteMaskMV':(40,41)}
for section in ['m_Floats','m_Colors','m_TexEnvs']:
    old=reader.property_table(authored[section]);new=reader.property_table(current[section])
    for name,value in old.items():
        assert name in new,(section,name)
        if new[name]!=value:
            assert section=='m_Floats' and name in expected_stencil_changes and (value,new[name])==expected_stencil_changes[name],(section,name,value,new[name])
            migration_changes.append(dict(name=name,source=value,private_import=new[name],equivalence_qualified=False))
for name,value in floats.items():
    if name not in props:inactive.append(name);continue
    p=props[name];assert p['type'] in ['Float','Range','Int']
    expected=reader.property_table(current['m_Floats'])[name]
    assert bits(expected)==bits(p['value_float']),name
    checks.append(dict(name=name,kind='float',native_matches_private_float32=True,native_matches_authored_float32=bits(value)==bits(p['value_float']),source_value=value,native_value=p['value_float']))
for name,value in colors.items():
    if name not in props:inactive.append(name);continue
    assert all(bits(value[a])==bits(props[name]['value_vector'][b]) for a,b in zip('rgba','xyzw')),name
    checks.append(dict(name=name,kind='vector',float32_bits_verified=True))
for name,value in textures.items():
    if name not in props:inactive.append(name);continue
    p=props[name];ref=value['m_Texture']
    assert all(bits(value['m_Scale'][k])==bits(p['texture_scale'][k]) and bits(value['m_Offset'][k])==bits(p['texture_offset'][k]) for k in 'xy'),name
    if ref['fileID']!=0:
        assert not p['texture_null'] and ref['guid']==p['texture_guid'] and str(ref['fileID'])==p['texture_file_id'],name
    checks.append(dict(name=name,kind='texture',source_reference=ref,native_binding=p,explicit_reference_verified=ref['fileID']!=0))
objects,byid=reader.objects(graph_path);root=next(o for o in objects if o['m_Type'].endswith('.GraphData'))
assert len(root['m_ActiveTargets'])==1
target=byid[root['m_ActiveTargets'][0]['m_Id']];sub=byid[target['m_ActiveSubTarget']['m_Id']]
data=[byid[r['m_Id']] for r in target['m_Datas']]
shaderpath=B/'source-generated.shader';shader=shaderpath.read_text('utf-8');passes=[]
for block in blocks(shader):
    header=block.split('HLSLPROGRAM',1)[0];name=re.search(r'\bName\s+"([^"]+)"',header);assert name
    state={}
    for key in ['Cull','ZTest','ZWrite','AlphaToMask','ColorMask','Blend']:
        values=re.findall(r'^\s*'+key+r'\s+([^\r\n]+)',header,re.M)
        assert key in ['ColorMask','Blend'] or len(values)<=1
        if values:state[key]=[v.strip() for v in values] if key in ['ColorMask','Blend'] else values[0].strip()
    referenced=re.findall(r'\[([^\]]+)\]',header)
    resolved={name:props[name]['value_float'] for name in referenced if name in props and props[name]['type'] in ['Float','Range','Int']}
    passes.append(dict(name=name[1],state=state,full_generated_header=header,native_resolved_float_properties=resolved,unresolved_property_references=sorted(set(referenced)-set(resolved))))
assert passes and '_ALPHATOMASK_ON' in source['m_ValidKeywords']
assert '_ALPHATOMASK_ON' in private['m_InvalidKeywords'] and not m['alpha_to_mask_keyword_declared']
for p in [Path(__file__),S/'read_delta_foliage_render_contracts_v1.py',B/'PREPARED.v1.json',B/'RUN-RESULT.v1.json',B/'NATIVE-RESULT.v1.json',shaderpath,original_path,graph_path,private_path]:pins[str(p)]=sha(p)
for row in prep['private_copies']:assert sha(row['copy'])==row['private_before_sha256'],row['copy']
O.mkdir()
result=dict(status='authored_palm_properties_and_native_generated_pass_contract_reopened',material_guid=m['guid'],authored_saved_properties=authored,private_saved_properties=current,explicit_unqualified_stencil_changes=migration_changes,authored_property_checks=checks,authored_not_declared_by_current_shader=inactive,source_valid_keywords=source['m_ValidKeywords'],private_valid_keywords=private['m_ValidKeywords'],private_invalid_keywords=private['m_InvalidKeywords'],native_declared_keywords=m['declared_keywords'],native_material=m,original_active_target=target,original_active_subtarget=sub,original_active_data=data,generated_pass_states=passes,generated_pass_count=len(passes),alpha_to_mask_keyword_migration_confirmed=True,source_version_render_equivalence_qualified=False,thumbnail_admission_qualified=False,shader_gpu_qualified=False,full_import=False,input_pins=pins,finished_utc=datetime.now(timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps(dict(status=result['status'],authored_properties_checked=len(checks),inactive_authored_properties=len(inactive),passes=len(passes),alpha_to_mask_passes=[p for p in passes if 'AlphaToMask' in p['state']])))
