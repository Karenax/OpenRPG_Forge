"""Derive the existing emitter using native output widths and complete source ports."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,subprocess,sys
W=Path("F:/OpenRPG_ImportWork");ROOT=W/"townsmith-s-paint-wgsl-v1"
PARENT=W/"native-terracotta-urp-v1/emit_typed_program_v1.py"
IR=W/"townsmith-s-paint-source-program-v1/material-family-active-ir-v1/abc00000000009486088228754689894.json"
TYPES=W/"townsmith-s-paint-native-types-v3/RESULT.v1.json"
PACK=W/"unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache/com.unity.render-pipelines.core@a4c9a9793789/ShaderLibrary/Packing.hlsl"
TOOLS=W/"native-hdrp-nine-source-programs-v1/source-graph-real-lighting-composer-v2"
def sha(p):
    with Path(p).open("rb") as f:return hashlib.file_digest(f,"sha256").hexdigest()
def read(p):return json.loads(p.read_text(encoding="utf-8-sig"))
def main():
    assert not ROOT.exists()
    types=read(TYPES);assert types["nodes_typed"]==134 and not types["missing"] and not types["ambiguous"]
    assert all(r["precision"]=="float" for r in types["rows"])
    for p,h in types["input_pins"].items():assert sha(p)==h,p
    text=PARENT.read_text(encoding="utf-8")
    a=text.index("B=Path(");b=text.index("\n",a)
    text=text[:a]+f"B=Path({str(ROOT)!r});P=Path({str(IR)!r});ir=json.loads(P.read_text('utf8'));O=B/'typed-program-v1';assert not O.exists();O.mkdir()"+text[b:]
    text=text.replace("width={};lines=[];textures={};casts=[]",f"native_types=json.loads(Path({str(TYPES)!r}).read_text());native_width={{r['ir_id']:r['width'] for r in native_types['rows']}};width={{}};lines=[];textures={{}};casts=[];normal_textures={{}}")
    text=text.replace("return [v[k] for k in ('x','y','z','w') if k in v]","return [v[k] for k in (('r','g','b','a') if 'r' in v else ('x','y','z','w')) if k in v]")
    old="""   assert out==0 and node['source_parameters']['m_NormalMapSpace']==0;expr='vec4<f32>(source_normal_ts_decoded,1.0)';w=4"""
    assert text.count(old)==1
    text=text.replace(old,"""   assert out==0 and node['source_parameters']['m_NormalMapSpace']==0
   tid=textures.setdefault(guid,len(textures));nid=normal_textures.setdefault(guid,len(normal_textures))
   expr=f'source_unpack_normal_sample(textureSample(t{tid},s{tid},{uv}),normal_unpack_modes[{nid}])';w=4""")
    text=text.replace("w=max(raw(ins[x]['value'])[1] for x in keys)","w=native_width[idx]")
    text=text.replace(" width[idx]=w;lines.append(", " assert w==native_width[idx],(idx,k,w,native_width[idx])\n width[idx]=w;lines.append(")
    anchor=" else:raise ValueError('Untranslated source operation: '+k)"
    assert text.count(anchor)==1
    text=text.replace(anchor,""" elif k=='SaturateNode':
  a,w=input_value(ins,'In',native_width[idx]);expr=f'clamp({a},{literal([0]*w)},{literal([1]*w)})'
 elif k=='ClampNode':
  w=native_width[idx];args=[input_value(ins,x,w)[0] for x in ('In','Min','Max')];expr='clamp('+','.join(args)+')'
 elif k in ('AddNode','SubtractNode'):
  w=native_width[idx];a,_=input_value(ins,'A',w);b,_=input_value(ins,'B',w);expr='('+a+('+' if k=='AddNode' else '-')+b+')'
 elif k=='DotProductNode':
  a,_=input_value(ins,'A',3);b,_=input_value(ins,'B',3);w=1;expr=f'dot({a},{b})'
 elif k=='TransformNode':
  assert node['source_parameters']['m_Conversion']=={'from':0,'to':4} and node['source_parameters']['m_ConversionType']==0
  a,_=input_value(ins,'In',3);w=3;expr=f'(source_object_to_absolute_world*vec4<f32>({a},1.0)).xyz'
"""+anchor)
    anchor="fields=[('base_color'"
    a=text.index(anchor);b=text.index("\n",a)
    text=text[:a]+"""fields=[('base_color','SurfaceDescription.BaseColor',3),('normal_ts','SurfaceDescription.NormalTS',3),('bent_normal_ts','SurfaceDescription.BentNormal',3),('metallic','SurfaceDescription.Metallic',1),('emission','SurfaceDescription.Emission',3),('smoothness','SurfaceDescription.Smoothness',1),('occlusion','SurfaceDescription.Occlusion',1),('alpha','SurfaceDescription.Alpha',1)]"""+text[b:]
    text=text.replace("source_normal_ts_decoded:vec3<f32>","source_object_to_absolute_world:mat4x4<f32>,normal_unpack_modes:vec3<u32>,source_tangent_space_normal:vec3<f32>")
    # The unconnected bent-normal slot is an implicit source input, not serialized zero.
    text=text.replace("ret=[convert(*raw(ir['terminals'][terminal]),w) for _,terminal,w in fields]","assert ir['terminals']['SurfaceDescription.BentNormal']['source_slot']['m_Type'].endswith('.NormalMaterialSlot')\nret=[('source_tangent_space_normal' if terminal=='SurfaceDescription.BentNormal' else convert(*raw(ir['terminals'][terminal]),w)) for _,terminal,w in fields]")
    helper="""fn source_unpack_normal_sample(packed:vec4<f32>,mode:u32)->vec4<f32> {
    if mode==0u { return vec4<f32>(packed.rgb*2.0-vec3<f32>(1.0),packed.a); }
    if mode==1u || mode==2u {
        var x=packed.a;
        if mode==1u { x*=packed.r; }
        let xy=vec2<f32>(x,packed.g)*2.0-vec2<f32>(1.0);
        let z=max(1.0e-16,sqrt(1.0-clamp(dot(xy,xy),0.0,1.0)));
        return vec4<f32>(xy,z,packed.a);
    }
    return vec4<f32>(bitcast<f32>(0x7fc00000u));
}
"""
    text=text.replace("header=['// Source graph",f"header=[{helper!r},'// Source graph")
    text=text.replace("'normal_decode_delegated_to_source_importer':True","'normal_decode_delegated_to_source_importer':False,'normal_texture_indices':normal_textures,'normal_unpack_profiles_required':True,'native_widths_used':True")
    compile(text,"derived-emitter","exec")
    # All raw source shader evidence remains untouched.
    ROOT.mkdir();emitter=ROOT/"emit_typed_program.v1.py";emitter.write_text(text,encoding="utf-8")
    child=subprocess.run([sys.executable,"-B",str(emitter)],capture_output=True,creationflags=subprocess.CREATE_NO_WINDOW)
    (ROOT/"emitter.stdout.log").write_bytes(child.stdout);(ROOT/"emitter.stderr.log").write_bytes(child.stderr)
    result={"status":"townsmith_paint_wgsl_emission_refusal_retained","finished_utc":datetime.now(timezone.utc).isoformat(),"emitter_exit_code":child.returncode,"source_equivalence_gpu_qualified":False,"forge_material_render_qualified":False,"full_import":False,"complete_assets_delivered_increment":0,"input_pins":{str(p):sha(p) for p in [PARENT,IR,TYPES,PACK,emitter,Path(__file__)]}}
    if child.returncode==0:
        generated=read(ROOT/"typed-program-v1/GENERATION.v1.json")
        assert generated["translated_operations"]==134 and len(generated["normal_texture_indices"])==3
        shader=ROOT/"typed-program-v1/material-program.wgsl"
        probe=ROOT/"fragment-type-admission.wgsl"
        probe.write_text(shader.read_text()+"""
@fragment fn type_admission_probe(@location(0) uv:vec4<f32>)->@location(0) vec4<f32> {
    let m=mat4x4<f32>(vec4<f32>(1.0,0.0,0.0,0.0),vec4<f32>(0.0,1.0,0.0,0.0),vec4<f32>(0.0,0.0,1.0,0.0),vec4<f32>(0.0,0.0,0.0,1.0));
    let v=evaluate_source_material(uv,vec3<f32>(0.0),vec3<f32>(0.0,1.0,0.0),m,vec3<u32>(1u),vec3<f32>(0.0,0.0,1.0));
    return vec4<f32>(v.base_color,v.alpha);
}
""",encoding="utf-8")
        tool=TOOLS/"validate_real_bevy_closure.exe";modules=TOOLS/"module-map.tsv";prior=read(TOOLS/"RESULT.v1.json")
        assert sha(tool)==prior["exe_sha256"] and sha(modules)==prior["module_map_sha256"]
        process=subprocess.run([str(tool),str(modules),str(probe)],capture_output=True,creationflags=subprocess.CREATE_NO_WINDOW)
        (ROOT/"validator.stdout.log").write_bytes(process.stdout);(ROOT/"validator.stderr.log").write_bytes(process.stderr)
        result.update(status="townsmith_paint_all_active_wgsl_operations_generated_type_admission_pending" if process.returncode else "townsmith_paint_all_active_wgsl_operations_generated_and_fragment_types_validated",validator_exit_code=process.returncode,translated_operations=134,texture_bindings=len(generated["textures"]),normal_texture_bindings=3,shader=str(shader),shader_sha256=sha(shader),mandatory_original_inputs=["original UV0 float4","absolute world position","world normal","object-to-absolute-world matrix","three validated importer normal-format profiles","tangent-space normal for implicit bent-normal output"],limits=["Arithmetic module and type admission only, not a complete Forge material or source-GPU fidelity proof.","Original texture packing, sRGB, mips, sampler modes and native GPU comparison remain unqualified.","Fragment type probe was validated, not executed; its constants are not runtime defaults."])
        for p in [shader,probe,tool,modules,ROOT/"typed-program-v1/GENERATION.v1.json"]:result["input_pins"][str(p)]=sha(p)
        if process.returncode:print(process.stderr.decode("utf-8",errors="replace"))
    else:print(child.stderr.decode("utf-8",errors="replace"))
    for p,h in result["input_pins"].items():assert sha(p)==h,p
    (ROOT/"RESULT.v1.json").write_text(json.dumps(result,indent=2),encoding="utf-8")
    print(json.dumps({k:v for k,v in result.items() if k!="input_pins"}))
if __name__=="__main__":main()
