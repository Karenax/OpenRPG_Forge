"""Bind the measured DXT5nm recipe to six surface candidates, preserving old stages."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,re,subprocess
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'house-native-bound-surface-candidates-v615'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def block(text,start):
    opening=text.index('{',start);depth=1
    for i in range(opening+1,len(text)):
        depth+=(text[i]=='{')-(text[i]=='}')
        if depth==0:return text[start:i+1]
    raise ValueError('Unclosed WGSL helper')
assert not O.exists()
np=W/'house-normal-decode-gpu-v614/RESULT.v1.json';normal=read(np);assert normal['failed_components']==0 and normal['native_dxt5nm_decode_verified'] and normal['native_gpu_color_and_scalar_bindings_verified']
tp=W/'house-surface-candidates-v611/RESULT.v1.json';top=read(tp);assert top['materials_composed']==6 and not top['failures']
pins={}
for proof in [normal,top]:
    for k in ['input_pins','output_pins']:
        for p,h in proof[k].items():assert sha(p)==h,p;pins[p]=h
bindingpath=W/'house-normal-decode-gpu-v614/NATIVE-BINDINGS.v1.json';bindings={m['material_guid']:m for m in read(bindingpath)}
toolroot=W/'native-hdrp-nine-source-programs-v1/source-graph-real-lighting-composer-v2';tool=toolroot/'validate_real_bevy_closure.exe';modules=toolroot/'module-map.tsv';toolproof=read(toolroot/'RESULT.v1.json');assert sha(tool)==toolproof['exe_sha256'] and sha(modules)==toolproof['module_map_sha256']
for p in [Path(__file__),np,tp,bindingpath,tool,modules,toolroot/'RESULT.v1.json']:pins[str(p)]=sha(p)
O.mkdir();rows=[];outputs={}
for material in top['rows']:
    guid=material['material_guid'];case=O/guid;case.mkdir();source=Path(material['wgsl']);assert sha(source)==material['wgsl_sha256'];text=source.read_text('utf-8-sig')
    start=text.index('fn source_graph_normal_sample(');old=block(text,start);assert old.count('source_decode_normal(encoded.rgb,mode)')==1
    # The new mode is specific to measured imported RG/AG storage; existing modes remain unchanged.
    addition='''
fn source_native_rg_ag_normal_sample(encoded:vec4<f32>)->vec4<f32>{
 let alpha=encoded.a*encoded.r;
 let xy=vec2<f32>(alpha,encoded.g)*2.0-vec2<f32>(1);
 let z=max(1.0e-16,sqrt(1.0-clamp(dot(xy,xy),0.0,1.0)));
 return vec4<f32>(xy,z,encoded.a);
}
'''
    new=old.replace('{','{\n    if(mode==2.0){return source_native_rg_ag_normal_sample(encoded);}',1);text=text.replace(old,addition+new)
    normal_binding=next(t for t in material['texture_bindings'] if t['texture_guid']==bindings[guid]['normal_guid']);index=normal_binding['texture_binding']//2;field='normal_mode_'+str(index)
    assert field in text
    output=case/'source_material.wgsl';output.write_text(text,'utf-8')
    manifest=dict(material_guid=guid,source_ir_sha256=material['wgsl_sha256'],normal_texture_guid=bindings[guid]['normal_guid'],native_normal_storage='RGBA_DXT5_UNorm',required_inputs={field:2.0},gpu_tint=bindings[guid]['gpu_tint'],native_scalar_uniforms=bindings[guid]['scalar_uniforms'],native_normal_numeric_proof=str(np),source_target='Unity 6000.5.8f1 D3D11 linear float/RG-or-AG decode',entire_surface_gpu_verified=False,host_binding_installed=False)
    mp=case/'NATIVE-BINDINGS.v1.json';mp.write_text(json.dumps(manifest,indent=2),'utf-8')
    probe=case/'surface-type-probe.wgsl';probe.write_text(text+f'''
@fragment fn house_surface_type_probe(@location(0) uv:vec2<f32>)->@location(0) vec4<f32>{{
 var i:SourceMaterialInputs;i.uv0=vec4<f32>(uv,0,0);i.view_direction_ts=vec3<f32>(0.2,0.1,1);i.inverse_object_xz_scale=vec2<f32>(1);i.normal_ts=vec3<f32>(0,0,1);i.{field}=2;
 let r=evaluate_original_source_material(i);return vec4<f32>(r.base_color,r.alpha);
}}
''','utf-8')
    run=subprocess.run([str(tool),str(modules),str(probe),'MATERIAL_BIND_GROUP=0'],capture_output=True,creationflags=subprocess.CREATE_NO_WINDOW);(case/'naga.stdout.log').write_bytes(run.stdout);(case/'naga.stderr.log').write_bytes(run.stderr);assert run.returncode==0 and b'PASS_REAL_BEVY0191_NAGA_OIL_COMPOSITION' in run.stdout
    for p in [output,mp,probe]:outputs[str(p)]=sha(p)
    rows.append(dict(material_guid=guid,wgsl=str(output),wgsl_sha256=sha(output),native_binding_manifest=str(mp),required_normal_mode=2,actual_naga_exit_code=0,source_texture_bindings=material['texture_bindings']))
for p,h in pins.items():assert sha(p)==h,p
result=dict(status='house_six_surface_candidates_native_normal_bound',utc=datetime.now(timezone.utc).isoformat(),materials=6,prefab_cards=24,rows=rows,input_pins=pins,output_pins=outputs,native_normal_decode_numeric_verified=True,native_gpu_color_scalar_bindings_verified=True,complete_surface_gpu_verified=False,forge_host_binding_installed=False,forge_render_verified=False,full_import=False,complete_conversion_increment=0)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
header='''## House — ressources GPU et normales raccordées (v615)

- Six matériaux : textures natives et valeurs couleur/scalaires réellement reçues par le GPU mesurées sans mutation des sources (v613).
- Normales DXT5nm/RG-or-AG : 72 échantillons, 288 valeurs comparées Unity/WGSL, aucun écart (v614). Cible Unity D3D11 : float, espace linéaire.
- Six candidats de surface raccordés au décodage natif correct et recomposés Naga/Bevy ; manifeste de liaison explicite pour le moteur (v615).
- Suite : exécuter et comparer la surface complète, installer les liaisons/profondeur moteur, qualifier les 24 prefabs/colliders puis Studio.
- Incrément de conversions complètes : **0**.

'''
(O/'LOT.md').write_text(header,'utf-8')
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;before=p.read_bytes();(O/(name+'.before')).write_bytes(before);p.write_text(header+before.decode('utf-8-sig'),'utf-8')
print(json.dumps({k:result[k] for k in ['status','materials','native_normal_decode_numeric_verified','complete_conversion_increment']}))
