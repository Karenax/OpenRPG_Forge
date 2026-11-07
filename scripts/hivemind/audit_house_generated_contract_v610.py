"""Reconcile active source IR with actual generated Forward graph/native bindings."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,re,struct
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'house-generated-contract-audit-v610';N=W/'house-generated-material-contract-v609';T=W/'house-main-material-lot-v603'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def f32(x):return struct.unpack('<f',struct.pack('<f',x))[0]
def block(text,start):
    opening=text.index('{',start);depth=1
    for i in range(opening+1,len(text)):
        depth+=(text[i]=='{')-(text[i]=='}')
        if depth==0:return text[start:i+1]
    raise ValueError('Unclosed generated source block')
assert not O.exists()
run=read(N/'RUN-RESULT.v1.json');assert run['unity_exit_code']==0 and not run['source_changes'] and not run['private_importer_changes']
pins=dict(read(N/'PREPARED.v1.json')['input_pins'])
for p,h in pins.items():assert sha(p)==h,p
native=read(N/'NATIVE-RESULT.v1.json');topology=read(T/'RESULT.v1.json');shaderpath=N/'source-generated.shader';text=shaderpath.read_text('utf-8-sig')
passes=[block(text,m.start()) for m in re.finditer(r'\bPass\s*\{',text)];forward=[p for p in passes if re.search(r'\bName\s+"Forward"',p.split('HLSLPROGRAM',1)[0])];assert len(forward)==1;forward=forward[0]
definition=re.search(r'\bSurfaceDescription SurfaceDescriptionFunction\(SurfaceDescriptionInputs IN\)',forward);assert definition
surface=block(forward,definition.start());assert not re.search(r'\bhalf[234]?\b',surface)
active_pom='b30810784af04338acbc6c489bc06f66'
assert f'#define POM_NAME_ID ParallaxOcclusionMapping_{active_pom}_float' in forward
callback=re.search(r'float ComputePerPixelHeightDisplacement_ParallaxOcclusionMapping_'+active_pom+r'\(',forward);assert callback;callback=block(forward,callback.start());assert re.search(r'SAMPLE_TEXTURE2D_LOD\([^\n]+\)\[0\]',callback)
pomcall=next(l for l in surface.splitlines() if f'float2 _ParallaxOcclusionMapping_{active_pom}_ParallaxUVs_1_Vector2' in l)
assert 'UnityBuildSamplerStateStruct(SamplerState_Linear_Repeat).samplerstate' in pomcall
normal_lines=[l for l in surface.splitlines() if '42064dbff1d048fe8162015f36ebfe5c' in l and 'SAMPLE_TEXTURE2D' in l];assert len(normal_lines)==1 and '_UV_720630f20be04edb86fc71f381e6bdca_Out_0_Vector4.xy' in normal_lines[0] and '_ParallaxOcclusionMapping_' not in normal_lines[0]
rows=[];errors=[]
for material in native['materials']:
    guid=material['guid'];irpath=T/'material-family-active-ir-v1'/(guid+'.json');expected=topology['output_pins'][str(irpath)];assert sha(irpath)==expected;pins[str(irpath)]=expected;ir=read(irpath)
    props={p['name']:p for p in material['properties']};values=[]
    def visit(v):
        if isinstance(v,dict):
            if v.get('provenance','').startswith('material:'):values.append(v)
            else:
                for child in v.values():visit(child)
        elif isinstance(v,list):
            for child in v:visit(child)
    visit(ir);checked={}
    for value in values:
        name=value['provenance'].split(':',1)[1];literal=value['literal'];prop=props.get(name);assert prop is not None,('Missing native property',guid,name)
        if isinstance(literal,dict) and 'guid' in literal:
            ok=prop['texture_guid']==literal['guid'] and [prop['texture_scale'][k] for k in ['x','y']]==[f32(x) for x in literal['scale']] and [prop['texture_offset'][k] for k in ['x','y']]==[f32(x) for x in literal['offset']]
            kind='native_texture_guid_and_transform'
        elif isinstance(literal,list):
            ok=[prop['value_vector'][k] for k in ['x','y','z','w'][:len(literal)]]==[f32(x) for x in literal];kind='native_color_api_value_not_gpu_uniform_qualification'
        else:ok=prop['value_float']==f32(literal);kind='native_float_api_value'
        checked[name]=dict(matches=ok,kind=kind)
        if not ok:errors.append(dict(material_guid=guid,property=name,source=literal,native=prop))
    assert len(ir['selected_branches'])==4 and props['_LayerBlend']['value_float']==0
    rows.append(dict(material_guid=guid,checked_properties=checked,selected_layer_branches_native_false=True,generated_surface_wrapper_float=True))
for p in [Path(__file__),N/'RUN-RESULT.v1.json',N/'PREPARED.v1.json',N/'NATIVE-RESULT.v1.json',shaderpath,T/'RESULT.v1.json']:pins[str(p)]=sha(p)
O.mkdir();(O/'generated-forward.shader').write_text(forward,'utf-8');(O/'generated-surface-function.hlsl').write_text(surface,'utf-8');(O/'generated-active-pom-height-callback.hlsl').write_text(callback,'utf-8')
outputpins={str(p):sha(p) for p in O.iterdir() if p.is_file()}
result=dict(status='house_active_bindings_and_generated_float_surface_verified' if not errors else 'house_generated_contract_mismatches_retained',utc=datetime.now(timezone.utc).isoformat(),materials=6,prefab_cards=24,rows=rows,errors=errors,generated_forward_surface_has_no_half_types=True,generated_pom_wrapper_float=True,active_pom_height_channel='red',active_pom_sampler='SamplerState_Linear_Repeat',normal_uv='original_uv0_with_own_texture_transform_no_parallax',native_color_api_is_not_gpu_uniform_proof=True,pom_sdk_real_macro_target_qualification_pending=True,actual_gpu_generated_surface_execution_verified=False,material_conversion_qualified=False,full_import=False,complete_conversion_increment=0,input_pins=pins,output_pins=outputpins,next_steps=['Verify SDK real precision target defines and actual surface GPU uniforms','Adapt existing node emitter to the qualified POM kernel','Compare complete generated surface with WGSL on actual decoded texture samples','Qualify Forge host depth and prefab rendering, then Studio'])
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
header='''## House — shader généré et liaisons natives vérifiés (v610)

- 6 matériaux : propriétés actives, GUID des textures, transformations et quatre branches de mélange comparés au banc Unity.
- Surface générée et wrapper POM en float ; sampler POM explicite Linear/Repeat ; normales sur leurs propres UV sans parallax.
- La comparaison POM v608 reste valide dans son domaine float explicite. Précision interne SDK selon cible et valeurs couleur au GPU restent à mesurer.
- Restent surface GPU complète, profondeur moteur, 24 prefabs/colliders et Studio. Incrément de conversions complètes : **0**.

'''
if not errors:
    for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
        path=W/'SUIVI_LOTS_HIVEMIND'/name;before=path.read_bytes();(O/(name+'.before')).write_bytes(before);path.write_text(header+before.decode('utf-8-sig'),'utf-8')
print(json.dumps({k:result[k] for k in ['status','materials','errors','generated_pom_wrapper_float','active_pom_sampler']}))
