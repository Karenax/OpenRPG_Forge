"""Compose all twelve House terminals using the existing emitter and pinned POM."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,importlib.util,subprocess,traceback
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'house-surface-candidates-v611'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
auditpath=W/'house-generated-contract-audit-v610/RESULT.v1.json';audit=read(auditpath);assert not audit['errors']
pins=dict(audit['input_pins'])
for p,h in pins.items():assert sha(p)==h,p
spec=importlib.util.spec_from_file_location('existing_house_node_emitter',S/'generate_nested_material_wgsl.py');g=importlib.util.module_from_spec(spec);spec.loader.exec_module(g)
toolroot=W/'native-hdrp-nine-source-programs-v1/source-graph-real-lighting-composer-v2';tool=toolroot/'validate_real_bevy_closure.exe';modules=toolroot/'module-map.tsv';toolproof=read(toolroot/'RESULT.v1.json');assert sha(tool)==toolproof['exe_sha256'] and sha(modules)==toolproof['module_map_sha256']
oldkernel=g.KERNEL.read_text('utf-8-sig');marker='// Original retained PNG math';assert oldkernel.count(marker)==1
normalhelpers=oldkernel[oldkernel.index(marker):]
pompath=S/'candidates/house_source_pom_v604.wgsl';pom=pompath.read_text('utf-8-sig')
for line in ['@group(0) @binding(0) var height_texture: texture_2d<f32>;','@group(0) @binding(1) var height_sampler: sampler;']:
    assert pom.count(line)==1;pom=pom.replace(line,'')
pom=pom.replace('SourcePomResult','SourcePomFloatResult')
pom=pom.replace('fn source_height(uv:', 'fn source_height(tex:texture_2d<f32>, smp:sampler, uv:').replace('textureSampleLevel(height_texture, height_sampler,','textureSampleLevel(tex, smp,')
pom=pom.replace('fn source_pom_search(uv:', 'fn source_pom_search(tex:texture_2d<f32>, smp:sampler, uv:')
pom=pom.replace('source_height(uv +','source_height(tex, smp, uv +')
pom=pom.replace('fn evaluate_source_pom(uv:', 'fn evaluate_source_pom(tex:texture_2d<f32>, smp:sampler, uv:').replace('source_pom_search(source_uv,','source_pom_search(tex, smp, source_uv,')
bridge='''
struct SourcePomResult {uv:vec2<f32>,depth_offset:f32,height:f32,}
fn source_pom_node(tex:texture_2d<f32>,smp:sampler,uv:vec2<f32>,tiling:vec2<f32>,offset:vec2<f32>,primitive_size:vec2<f32>,texture_scale:vec2<f32>,texture_offset:vec2<f32>,amplitude_cm:f32,steps_input:f32,lod:f32,lod_threshold:f32,view_dir_ts:vec3<f32>,inverse_object_xz_scale:vec2<f32>)->SourcePomResult{
 let matrix=mat4x4<f32>(vec4<f32>(inverse_object_xz_scale.x,0,0,0),vec4<f32>(0,1,0,0),vec4<f32>(0,0,inverse_object_xz_scale.y,0),vec4<f32>(0,0,0,1));
 let r=evaluate_source_pom(tex,smp,uv,view_dir_ts,matrix,amplitude_cm,steps_input,tiling,offset,primitive_size,texture_scale,texture_offset,lod,lod_threshold);
 return SourcePomResult(r.uv,r.pixel_depth_offset,r.sampled_height);
}
'''
prefix=pom+bridge+normalhelpers+'\n'+g.EXTRA.read_text('utf-8-sig');emitter=g.EMITTER.read_text('utf-8-sig')
queuepath=W/'queue/house-on-a-hill/prefabs.json';queue=read(queuepath);topath=W/'house-main-material-lot-v603/RESULT.v1.json';top=read(topath)
for p in [Path(__file__),auditpath,S/'generate_nested_material_wgsl.py',g.KERNEL,g.EXTRA,g.EMITTER,pompath,tool,modules,toolroot/'RESULT.v1.json',queuepath,topath]:pins[str(p)]=sha(p)
O.mkdir();(O/'adapted-pom-library.wgsl').write_text(prefix,'utf-8');rows=[];failures=[];outputs={}
for material in top['rows']:
    guid=material['material_guid'];case=O/guid;case.mkdir()
    try:
        irpath=Path(material['ir_path']);assert sha(irpath)==material['ir_sha256'];pins[str(irpath)]=sha(irpath);ir=read(irpath);resources=[];normals=set()
        for node in ir['nodes']:
            for entry in node['inputs']:
                literal=entry['value'].get('literal')
                if not isinstance(literal,dict) or 'guid' not in literal:continue
                textureguid=literal['guid'];assert textureguid in queue['guid_paths']
                if textureguid not in resources:resources.append(textureguid)
                if node['opcode']=='SampleTexture2DNode' and node['source_parameters'].get('m_TextureType')==1:normals.add(textureguid)
        assert len(resources)==3 and len(normals)==1
        bindings=[]
        for textureguid in resources:
            p=Path(queue['source_root'])/queue['guid_paths'][textureguid]
            for source in [p,Path(str(p)+'.meta')]:pins[str(source)]=sha(source)
            bindings.append(dict(texture_guid=textureguid,texture_binding=2*resources.index(textureguid),sampler_binding=2*resources.index(textureguid)+1,normal_storage_decode_qualified=False if textureguid in normals else None,source=str(p)))
        ctx=dict(ir=ir,indices={t:i for i,t in enumerate(resources)},normal_guids=normals,dims={},body=[],input_requirements=[],prefix=prefix,case=case,scalar=g.scalar,vec=g.vec,math=g.math)
        exec(compile(emitter,str(g.EMITTER)+'#house-qualified-pom-library','exec'),ctx)
        output=case/'source_material.wgsl';probe=case/'surface-type-probe.wgsl';probe.write_text(output.read_text('utf-8-sig')+'''
@fragment fn house_surface_type_probe(@location(0) uv:vec2<f32>)->@location(0) vec4<f32>{
 var i:SourceMaterialInputs;i.uv0=vec4<f32>(uv,0,0);i.view_direction_ts=vec3<f32>(0.2,0.1,1);i.inverse_object_xz_scale=vec2<f32>(1);i.normal_ts=vec3<f32>(0,0,1);
 let r=evaluate_original_source_material(i);return vec4<f32>(r.base_color,r.alpha);
}
''','utf-8')
        run=subprocess.run([str(tool),str(modules),str(probe),'MATERIAL_BIND_GROUP=0'],capture_output=True,creationflags=subprocess.CREATE_NO_WINDOW);(case/'naga.stdout.log').write_bytes(run.stdout);(case/'naga.stderr.log').write_bytes(run.stderr)
        assert run.returncode==0 and b'PASS_REAL_BEVY0191_NAGA_OIL_COMPOSITION' in run.stdout,'Naga composition refused'
        outputs[str(output)]=sha(output);outputs[str(probe)]=sha(probe)
        rows.append(dict(material_guid=guid,texture_bindings=bindings,wgsl=str(output),wgsl_sha256=sha(output),terminal_fields=ctx['terminal_fields'],input_requirements=ctx['input_requirements'],actual_naga_exit_code=run.returncode,actual_surface_gpu_verified=False))
    except Exception as exc:
        (case/'failure.log').write_text(traceback.format_exc(),'utf-8');failures.append(dict(material_guid=guid,error=repr(exc)))
for p,h in pins.items():assert sha(p)==h,p
result=dict(status='house_six_surface_candidates_naga_composed' if not failures else 'house_surface_candidate_refusals_retained',utc=datetime.now(timezone.utc).isoformat(),materials_expected=6,materials_composed=len(rows),prefab_cards=24,rows=rows,failures=failures,input_pins=pins,output_pins=outputs,existing_node_emitter_unchanged=True,pom_float_library_adapted_from_numeric_qualified_v608=True,adapted_resource_parameter_bridge_gpu_qualification_pending=True,native_normal_decode_and_color_uniforms_pending=True,actual_surface_gpu_verified=False,forge_render_verified=False,material_conversion_qualified=False,complete_conversion_increment=0,full_import=False)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:result[k] for k in ['status','materials_composed','failures']}))
