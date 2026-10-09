"""Compose two original UV0-only static graphs against actual Bevy modules."""
from pathlib import Path

BASE=Path(__file__).with_name('build_delta_bevy_vertex_stages_v2.py')
SCRIPT=BASE.read_text('utf-8-sig')

def replace(old,new):
    global SCRIPT
    assert SCRIPT.count(old)==1, old
    SCRIPT=SCRIPT.replace(old,new)

replace("ROOT = W / 'delta-vector-bevy-vertex-stages-v2'", "ROOT = W / 'delta-rocks-firepit-bevy-vertex-v1'")
replace("stages_path = W / 'delta-vector-native-stages-v1/RESULT.v1.json'", "stages_path = W / 'delta-rocks-firepit-native-stages-v1/RESULT.v1.json'")
replace("assert stages['vertex_and_fragment_stage_probes_validated'] == len(stages['rows']) == 6",
        "assert stages['vertex_and_fragment_stage_probes_validated'] == len(stages['rows']) == 2")
replace("kernels_path = W / 'delta-vector-node-wgsl-v3/RESULT.v1.json'", "kernels_path = W / 'nested-prefab-delta-material-wgsl-v1/RESULT.v1.json'")
replace("    previous = W / 'delta-vector-bevy-vertex-stages-v1/RESULT.v1.json'\n    assert read(previous)['native_vertex_programs_composed'] == 0\n    pins[str(previous)] = sha(previous)",
        "    pins[str(BASE)] = sha(BASE)")
replace("            output = case / 'source_graph_vertex_and_prepass.wgsl'", """            fields=set(re.findall(r'\\b(\\w+):',re.search(r'struct SourceMaterialInputs \\{(.*?)\\}',program,re.S).group(1)))
            assert not {'uv1','source_world_to_object','source_camera_relative_offset'} & fields
            # These two source graphs consume UV0 only. Do not require an unused UV1 attribute.
            adapted_coords=adapted_coords.replace(' source_uv1:vec4<f32>,\\n','')
            adapted_coords=adapted_coords.replace('output.uv1=packet.source_uv1;\\n','')
            adapted_coords=adapted_coords.replace('output.source_world_to_object=', 'let source_world_to_object=')
            adapted_coords=adapted_coords.replace('output.source_world_to_object','source_world_to_object')
            adapted_coords=adapted_coords.replace('output.source_camera_relative_offset=abs_m[3].xyz-world_m[3].xyz;\\n','')
            local_wrapper=wrapper.replace(' @location(12) source_uv1:vec4<f32>,','')
            local_wrapper=local_wrapper.replace(' packet.source_uv1=vertex.source_uv1;','')
            assert 'source_uv1' not in adapted_coords+local_wrapper
            output = case / 'source_graph_vertex_and_prepass.wgsl'""")
replace("output.write_text(program + INVERSE + adapted_coords + wrapper, encoding='utf-8')",
        "output.write_text(program + INVERSE + adapted_coords + local_wrapper, encoding='utf-8')")
replace('required_original_uv_channels=2,', 'required_original_uv_channels=1,')
replace('original_uv1_shader_location=12,', 'original_uv0_shader_location=10, unused_uv1_not_required=True,')
replace('requested=6,','requested=2,')
exec(compile(SCRIPT,str(BASE),'exec'),{'__name__':'__main__','__file__':str(Path(__file__).resolve()),'BASE':BASE})
