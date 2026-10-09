"""Apply the measured diffuse kernel to the original diagnostic scene carrier."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib,json,struct
W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'firepit-diffuse-scene-case-v1'
ENGINE=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust/crates/forge_engine/src/shaders')
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not ROOT.exists()
    cp=W/'firepit-shadow-comparison-case-v1/CASE.v1.json';case=read(cp)
    fixture=Path(case['fixture']);assert sha(fixture)==case['fixture_sha256']
    proof_path=W/'firepit-combined-diffuse-gpu-reopen-v1/RESULT.v1.json';proof=read(proof_path)
    assert proof['combined_diffuse_kernel_qualified'] and not proof['scene_application_qualified']
    for p,h in proof['input_pins'].items():assert sha(p)==h,p
    helpers=[ENGINE/n for n in ['source_diffuse_probe.wgsl','source_preintegrated_brdf.wgsl','source_deferred_diffuse.wgsl']]
    helper_text='\n'.join(p.read_text('utf-8') for p in helpers)
    raw=fixture.read_bytes();n=struct.unpack_from('<I',raw,12)[0]
    doc=json.loads(raw[20:20+n]);binary=bytearray(raw[28+n:])
    def stage(ref):
        v=doc['bufferViews'][ref['buffer_view']];off=v.get('byteOffset',0)
        data=bytes(binary[off:off+v['byteLength']]);assert hashlib.sha256(data).hexdigest()==ref['sha256']
        return data.decode('utf-8').replace('\r\n','\n').replace('\r','\n')
    insertion='''
    // Explicit measured source diffuse. This diagnostic has no native ambient,
    // irradiance volume or environment map, so the source diffuse is added once.
    var source_scene_visibility=1.0;
#ifdef SCREEN_SPACE_AMBIENT_OCCLUSION
    source_scene_visibility=textureLoad(view_bindings::screen_space_ambient_occlusion_texture,vec2<i32>(in.frag_coord.xy),0i).r;
#endif
    let source_diffuse_fgd=source_preintegrated_brdf_diffuse(source_preintegrated_brdf_sample(NdotV,perceptual_roughness));
    indirect_light+=source_deferred_diffuse_lighting(
        source_preconvolved_diffuse_irradiance(surface.bent_normal_world),
        source_diffuse_fgd,surface.diffuse_color,surface.diffuse_occlusion,source_scene_visibility);
'''
    def lit(text):
        marker='    // Total light'
        assert text.count(marker)==1
        return text.replace(marker,insertion+'\n'+marker)+'\n'+helper_text
    originals={};deltas=[]
    for m in doc['materials']:
        c=m['extensions']['OPENRPG_source_graph'];ref=c['forward'];key=(ref['buffer_view'],ref['sha256'])
        if key not in originals:
            before=ref.copy();text=lit(stage(ref));data=text.encode()
            while len(binary)%4:binary.append(0)
            view=len(doc['bufferViews']);doc['bufferViews'].append(dict(buffer=0,byteOffset=len(binary),byteLength=len(data)));binary.extend(data)
            originals[key]=dict(buffer_view=view,sha256=hashlib.sha256(data).hexdigest())
            deltas.append(dict(original=before,derived=originals[key]))
        c['forward']=originals[key].copy()
    ground_contract=doc['materials'][0]['extensions']['OPENRPG_source_graph']
    ground_forward=stage(ground_contract['forward']);ground_prepass=stage(ground_contract['prepass'])
    old='return evaluate_original_source_surface(source_graph_surface_inputs_from_host(packet));'
    replacement='''var out:SourceMaterialSurfaceOutputs;
 out.base_color=vec3<f32>(1.0);out.normal_ts=vec3<f32>(0.0,0.0,1.0);out.bent_normal=out.normal_ts;
 out.metallic=0.0;out.emission=vec3<f32>(0.0);out.smoothness=0.0;out.occlusion=1.0;
 out.alpha=1.0;out.alpha_clip_threshold=0.0;return out;'''
    assert ground_forward.count(old)==ground_prepass.count(old)==1
    ground_forward=ground_forward.replace(old,replacement);ground_prepass=ground_prepass.replace(old,replacement)
    while len(binary)%4:binary.append(0)
    doc['buffers'][0]['byteLength']=len(binary)
    doc.setdefault('extras',{})['private_diffuse_scene_diagnostic']=dict(not_deliverable=True,original_fixture_sha256=case['fixture_sha256'])
    encoded=json.dumps(doc,separators=(',',':')).encode();encoded+=b' '*((-len(encoded))%4)
    output=struct.pack('<III',0x46546c67,2,28+len(encoded)+len(binary))+struct.pack('<II',len(encoded),0x4e4f534a)+encoded+struct.pack('<II',len(binary),0x004e4942)+binary
    ROOT.mkdir();derived=ROOT/'private-source-diffuse-scene.glb';derived.write_bytes(output)
    ground={}
    for name,text in [('forward',ground_forward),('prepass',ground_prepass)]:
        p=ROOT/f'private-receiver-{name}.wgsl';p.write_text(text,'utf-8');ground[name]=dict(path=str(p),sha256=sha(p))
    environment=W/'firepit-shadow-source-reference-v11/SOURCE-STATE-0.json'
    case.update(fixture=str(derived),fixture_sha256=sha(derived),diagnostic_only=True,
        diffuse_probe_coefficients=read(environment)['hdrp_diffuse_probe_buffer'],
        brdf_lookup_protocol=read(W/'source-brdf-lookup-case-v1/CASE.v1.json'),
        receiver_program=ground,source_ssao_algorithm_matched=False,
        target_ssao_settings=dict(quality='High',constant_object_thickness=0.25,source_radius_and_intensity_matched=False))
    (ROOT/'CASE.v1.json').write_text(json.dumps(case,indent=2),'utf-8')
    pins=[Path(__file__).resolve(),cp,fixture,proof_path,environment,*helpers,W/'source-brdf-lookup-case-v1/CASE.v1.json',ROOT/'CASE.v1.json',*[Path(v['path']) for v in ground.values()]]
    result=dict(status='original_geometry_diffuse_scene_and_receiver_prepared',created_utc=datetime.now(timezone.utc).isoformat(),
        private_fixture=str(derived),private_fixture_sha256=sha(derived),forward_deltas=deltas,
        mesh_geometry_changed=False,source_uniform_abi_changed=False,source_ssao_pixels_used_as_target_input=False,
        actual_scene_diffuse_qualified=False,full_import=False,editor_visual_verified=False,
        input_pins={str(p):sha(p) for p in pins})
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()
