"""Preserve actual source/pass state, including disagreements between mode and constants."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,importlib.util,json,re,struct
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'gladiator-render-contracts-v431'
spec=importlib.util.spec_from_file_location('contract_reader',S/'read_delta_foliage_render_contracts_v1.py');reader=importlib.util.module_from_spec(spec);spec.loader.exec_module(reader)
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def bits(x):return struct.pack('<f',float(x))
def blocks(text,pattern):
    for match in re.finditer(pattern,text):
        opening=text.index('{',match.start());depth=1
        for i in range(opening+1,len(text)):
            depth+=(text[i]=='{')-(text[i]=='}')
            if depth==0:yield text[opening+1:i];break
        else:raise ValueError('Unclosed source block')
assert not O.exists()
bindings=W/'gladiator-native-material-bindings-v406/RESULT.v1.json';nativepath=W/'gladiator-native-shader-defaults-v410/NATIVE-RESULT.v1.json';shaderpath=W/'gladiator-native-shader-defaults-v410/source-generated.shader'
materials={m['guid']:m for m in read(nativepath)['materials']};closure=read(W/'gladiator-lot-dependency-closure-v394/RESULT.v1.json')
graphpath=next(Path(r['source']) for r in closure['files'] if r['source'].endswith('.shadergraph'))
assert sha(graphpath)=='70b3f0ea3849a14fe88af3135d775c73a95e5d78b2d3a1fda0547966f863e0ee'
allobjects,byid=reader.objects(graphpath);root=next(o for o in allobjects if o['m_Type'].endswith('.GraphData'));assert len(root['m_ActiveTargets'])==1
target=byid[root['m_ActiveTargets'][0]['m_Id']];sub=byid[target['m_ActiveSubTarget']['m_Id']];data=[byid[r['m_Id']] for r in target['m_Datas']]
lit=[o for o in data if o['m_Type'].endswith('.HDLitData')];lighting=[o for o in data if o['m_Type'].endswith('.LightingData')];builtin=[o for o in data if o['m_Type'].endswith('.BuiltinData')]
assert len(lit)==len(lighting)==len(builtin)==1 and sub['m_Type'].endswith('.HDLitSubTarget')
assert lit[0]['m_MaterialType']==0 and lighting[0]['m_NormalDropOffSpace']==0 and not builtin[0]['m_DepthOffset']
shader=shaderpath.read_text('utf-8');passes=[]
for block in blocks(shader,r'\bPass\s*\{'):
    header=block.split('HLSLPROGRAM',1)[0];name=re.search(r'\bName\s+"([^"]+)"',header)
    assert name
    state={}
    for key in ['Cull','ZTest','ZWrite','AlphaToMask','ColorMask']:
        matches=re.findall(r'^\s*'+key+r'\s+([^\r\n]+)',header,re.M);assert len(matches)<=1
        if matches:state[key]=matches[0].strip()
    state['Blend']=[v.strip() for v in re.findall(r'^\s*Blend\s+([^\r\n]+)',header,re.M)]
    passes.append(dict(name=name[1],state=state))
assert passes and any(p['state'].get('AlphaToMask')=='[_AlphaCutoffEnable]' for p in passes)
pins={str(p):sha(p) for p in [Path(__file__),S/'read_delta_foliage_render_contracts_v1.py',bindings,nativepath,shaderpath,graphpath]};rows=[]
for row in read(bindings)['rows']:
    p=Path(row['source']);pins[str(p)]=sha(p);text=re.sub(r'^%.*\n','',p.read_text('utf-8-sig'),flags=re.M);text=re.sub(r'^--- !u!\d+ &-?\d+.*$','---',text,flags=re.M)
    docs=[d['Material'] for d in reader.yaml.safe_load_all(text) if isinstance(d,dict) and 'Material' in d];assert len(docs)==1;source=docs[0]
    f=reader.property_table(source['m_SavedProperties']['m_Floats']);c=reader.property_table(source['m_SavedProperties']['m_Colors']);m=materials[row['material_guid']];props={r['name']:r for r in m['properties']}
    used=['_SurfaceType','_AlphaCutoffEnable','_DoubleSidedEnable','_DoubleSidedNormalMode','_CullMode','_CullModeForward','_ZWrite','_ZTestDepthEqualForOpaque','_ZTestGBuffer','_SrcBlend','_DstBlend','_AlphaSrcBlend','_AlphaDstBlend']
    resolved={name:props[name]['value_float'] for name in used}
    for name,value in resolved.items():
        assert name in f and bits(value)==bits(f[name]),(row['name'],name)
    assert resolved['_SurfaceType']==0 and resolved['_ZWrite']==1
    constants=[props['_DoubleSidedConstants']['value_vector'][key] for key in 'xyz']
    assert all(bits(a)==bits(c['_DoubleSidedConstants'][key]) for a,key in zip(constants,'rgb'))
    assert constants==[1,1,-1]
    rows.append(dict(material_guid=row['material_guid'],name=row['name'],original_material=str(p),original_material_sha256=sha(p),original_graph=str(graphpath),original_graph_sha256=sha(graphpath),active_target=target,active_subtarget=sub,original_active_data=data,source_floats=f,source_colors=c,source_saved_properties=source['m_SavedProperties'],source_valid_keywords=source.get('m_ValidKeywords',[]),source_invalid_keywords_preserved=source.get('m_InvalidKeywords',[]),source_custom_render_queue=source['m_CustomRenderQueue'],source_disabled_shader_passes=source.get('disabledShaderPasses',[]),native_render_queue=m['render_queue'],native_pass_enable_observations=m['passes'],native_declared_keywords=m['declared_keywords'],source_keyword_alpha_test='_ALPHATEST_ON' in source.get('m_ValidKeywords',[]),source_keyword_alpha_to_mask='_ALPHATOMASK_ON' in source.get('m_ValidKeywords',[]),source_keyword_double_sided='_DOUBLESIDED_ON' in source.get('m_ValidKeywords',[]),source_keyword_transparent=False,hdrp_standard_active_data=True,hdrp_normal_tangent_space=True,hdrp_specular_occlusion_mode=lighting[0]['m_SpecularOcclusionMode'],source_depth_offset_enabled=False,original_double_sided_constants_xyz=constants if resolved['_DoubleSidedEnable']==1 else None,native_resolved_state=resolved,double_sided_mode_enum_vs_constants_consistent=False if resolved['_DoubleSidedEnable']==1 and resolved['_DoubleSidedNormalMode']==1 else None,backface_equation_must_use_native_constants=True,generated_pass_states=passes,alpha_to_coverage_must_be_resolved_per_pass=True,original_package_version_parity_qualified=False,material_render_parity_qualified=False))
assert len(rows)==6
for p,h in pins.items():assert sha(p)==h
O.mkdir();result=dict(status='six_source_and_native_render_contracts_preserved',updated_utc=datetime.now(timezone.utc).isoformat(),rows=rows,input_pins=pins,pass_state_count=len(passes),renderer_installed=False,gpu_rendered=False,full_import=False)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps(dict(status=result['status'],materials=6,generated_pass_states=len(passes),double_sided=sum(r['native_resolved_state']['_DoubleSidedEnable']==1 for r in rows),alpha_test=sum(r['native_resolved_state']['_AlphaCutoffEnable']==1 for r in rows))))
