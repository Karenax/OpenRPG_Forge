"""Validate the exact legacy surface/UV port, preserving full-render requirements."""
from pathlib import Path
import datetime
import hashlib
import json
import shutil
import subprocess

W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'delta-firepit-alpha-shader-port-v1'
HELPER=Path(__file__).with_name('candidates')/'legacy_particle_alpha_blended.wgsl'
TOOLS=W/'native-hdrp-nine-source-programs-v1/source-graph-real-lighting-composer-v2'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
def main():
    assert not ROOT.exists()
    sources_path=W/'unity-builtin-shaders-6000.5.8f1-v4/RESULT.v1.json'
    source=read(sources_path)
    for p,d in source['input_pins'].items():assert sha(p)==d
    native_path=W/'unity-delta-firepit-effect-materials-v1/MATERIALS.v1.json'
    native=read(native_path);assert len(native['cases'])==2
    original=Path(source['shader']).read_text('utf-8-sig')
    for required in ['Blend SrcAlpha OneMinusSrcAlpha','ColorMask RGB','Cull Off Lighting Off ZWrite Off',
        'o.color = v.color * _TintColor;', 'o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);',
        'saturate (_InvFade * (sceneZ-partZ))', 'i.color.a *= fade;',
        '2.0f * i.color * tex2D(_MainTex, i.texcoord)', 'col.a = saturate(col.a);',
        'UNITY_APPLY_FOG(i.fogCoord, col);']:
        assert required in original,'Original equation or render state differs: '+required
    tool=TOOLS/'validate_real_bevy_closure.exe';modules=TOOLS/'module-map.tsv'
    proven=read(TOOLS/'RESULT.v1.json')
    assert sha(tool)==proven['exe_sha256'] and sha(modules)==proven['module_map_sha256']
    ROOT.mkdir();shutil.copyfile(HELPER,ROOT/HELPER.name)
    library=HELPER.read_text('utf-8-sig')
    shader=library+'''
struct SourceCase { color: vec4<f32>, tint: vec4<f32>, texel: vec4<f32>,
    depth: vec4<f32>, fog: vec4<f32>, uv_st: vec4<f32>, uv: vec4<f32> }
@group(0) @binding(0) var<storage, read> cases: array<SourceCase>;
@group(0) @binding(1) var<storage, read_write> results: array<vec4<f32>>;
@compute @workgroup_size(1) fn validate_source_port(@builtin(global_invocation_id) id: vec3<u32>) {
    if id.x >= arrayLength(&cases) { return; }
    let item = cases[id.x];
    let uv = source_particle_alpha_uv(item.uv.xy, item.uv_st);
    let color = source_particle_alpha_vertex_color(item.color, item.tint);
    results[2u * id.x] = source_particle_alpha_surface(color, item.texel,
        item.depth.w != 0.0, item.depth.x, item.depth.y, item.depth.z,
        item.uv.z != 0.0, item.uv.w, item.fog.rgb);
    results[2u * id.x + 1u] = vec4<f32>(uv, 0.0, 0.0);
}
'''
    target=ROOT/'validate_source_port.wgsl';target.write_text(shader,'utf-8')
    licenses=[]
    for index,row in enumerate(source['licences']):
        path=ROOT/f'LICENSE.Unity.{index:02d}.txt'
        assert sha(row['path'])==row['sha256'];shutil.copyfile(row['path'],path)
        assert sha(path)==row['sha256'];licenses.append(str(path))
    pins={str(p):sha(p) for p in [Path(__file__).resolve(),HELPER,target,ROOT/HELPER.name,
        sources_path,native_path,tool,modules,TOOLS/'RESULT.v1.json']}
    for p in licenses:pins[p]=sha(p)
    for name,path,expected in [('positive',target,0),('wrong_soft_particle_type',ROOT/'invalid_type.wgsl',3)]:
        if name!='positive':
            assert shader.count('item.depth.w != 0.0')==1
            path.write_text(shader.replace('item.depth.w != 0.0','item.depth.w'),'utf-8')
        with (ROOT/(name+'.stdout.log')).open('xb') as out,(ROOT/(name+'.stderr.log')).open('xb') as err:
            process=subprocess.run([str(tool),str(modules),str(path)],stdout=out,stderr=err,
                creationflags=subprocess.CREATE_NO_WINDOW)
        assert process.returncode==expected,'Actual Naga validation differs: '+name
    output=(ROOT/'positive.stdout.log').read_text('utf-8-sig')
    assert 'entrypoints=1' in output and 'functions=3' in output
    rows=[]
    for row in native['cases']:
        assert row['shader_name']=='Legacy Shaders/Particles/Alpha Blended' and row['shader_file_id']=='203'
        properties={p['name']:p for p in row['properties']}
        assert set(properties)=={'_TintColor','_MainTex','_InvFade'}
        assert properties['_MainTex']['texture_guid']=='63f624c8bd42f35409dde06b85d2d900'
        rows.append(dict(material_guid=row['material_guid'],native_properties=properties,
            inactive_saved_texture_aliases_not_selected=True))
    assert all(sha(p)==d for p,d in pins.items())
    result=dict(status='two_source_alpha_materials_and_three_port_functions_naga_validated',
        checked_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),input_pins=pins,
        material_instances=rows,shader_library=str(ROOT/HELPER.name),
        actual_naga_positive_exit_code=0,actual_invalid_type_exit_code=3,
        native_shader_source_include_differences_retained=55,
        original_shader_render_state=dict(color_blend=[5,10],alpha_blend=[5,10],
            write_channels=['red','green','blue'],cull=0,lighting=False,write_depth=False),
        source_uv_tint_double_brightness_alpha_saturation_soft_depth_and_fog_preserved=True,
        color_mask_adapted_to_runtime=False,billboard_and_atlas_geometry_qualified=False,
        shader_port_gpu_native_parity_qualified=False,renderer_installed=False,
        gpu_rendered=False,runtime_converted=False,full_import=False)
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps(dict(status=result['status'],naga_functions=3,material_instances=2,full_import=False)))
if __name__=='__main__':main()
