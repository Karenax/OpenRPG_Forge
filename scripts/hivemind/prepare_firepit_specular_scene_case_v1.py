"""Apply the verified source specular kernel to original object and receiver."""
from pathlib import Path
import json,hashlib
W=Path('F:/OpenRPG_ImportWork')
specular_proof=W/'firepit-specular-fixed8-gpu-reopen-v1/RESULT.v1.json'
proof=json.loads(specular_proof.read_text('utf-8'))
assert proof['status']=='actual_gpu_deferred_specular_756_case_composition_verified'
for p,h in proof['input_pins'].items():
    with Path(p).open('rb') as f:assert hashlib.file_digest(f,'sha256').hexdigest()==h,p
BASE_SPECULAR_SCENE=Path(__file__).with_name('prepare_firepit_diffuse_scene_case_v1.py')
source=BASE_SPECULAR_SCENE.read_text('utf-8').replace('firepit-diffuse-scene-case-v1','firepit-specular-scene-case-v1').replace('private-source-diffuse-scene.glb','private-source-specular-scene.glb')
source=source.replace("'source_deferred_diffuse.wgsl']","'source_deferred_diffuse.wgsl','source_sky_cube.wgsl','source_deferred_specular.wgsl']")
addition='''
    // Use the source world for the Deferred normal encoding and sky direction.
    let source_specular_normal=(source_graph.source_world_from_target_world*vec4<f32>(surface.normal_world,0.0)).xyz;
    let source_specular_view=(source_graph.source_world_from_target_world*vec4<f32>(p.V,0.0)).xyz;
    indirect_light+=source_deferred_sky_specular(source_specular_normal,source_specular_view,
        perceptual_roughness,F0,surface.specular_occlusion,source_scene_visibility,1.0,1.0);
'''
end=source.index("'''\n    def lit",source.index("    insertion='''"))
source=source[:end]+addition+source[end:]
source=source.replace("receiver_program=ground,source_ssao_algorithm_matched=False,",
    "receiver_program=ground,source_ssao_algorithm_matched=False,sky_cube_packet=read(W/'source-sky-cube-packet-v1/PACKET.v1.json'),source_specular_proof=read(W/'firepit-specular-fixed8-gpu-reopen-v1/RESULT.v1.json'),")
source=source.replace('proof_path,environment,*helpers,',"proof_path,environment,W/'firepit-specular-fixed8-gpu-reopen-v1/RESULT.v1.json',W/'source-sky-cube-packet-v1/PACKET.v1.json',*helpers,")
source=source.replace('original_geometry_diffuse_scene_and_receiver_prepared','original_geometry_diffuse_and_specular_scene_receiver_prepared')
exec(compile(source,str(BASE_SPECULAR_SCENE),'exec'),globals())
