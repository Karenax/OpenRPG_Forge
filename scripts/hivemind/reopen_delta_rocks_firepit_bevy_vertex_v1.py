"""Independently reopen eight actual vertex compositions and five scoped refusals."""
from pathlib import Path
BASE=Path(__file__).with_name('reopen_delta_bevy_vertex_stages_v2.py')
SCRIPT=BASE.read_text('utf-8-sig')
SCRIPT=SCRIPT.replace("ROOT = W / 'delta-vector-bevy-vertex-stages-v2'", "ROOT = W / 'delta-rocks-firepit-bevy-vertex-v1'")
SCRIPT=SCRIPT.replace('== 6','== 2').replace('== 24','== 8')
start=SCRIPT.index("        assert source.count('@location(12)")
end=SCRIPT.index("        stages = row['actual_stage_compositions']",start)
SCRIPT=SCRIPT[:start]+"""        assert '@location(10) source_uv0:vec4<f32>,' in source
        assert 'packet.source_uv0=vertex.source_uv0;' in source
        assert 'output.uv0=packet.source_uv0;' in source
        assert 'source_uv1' not in source and 'output.uv1' not in source
        assert 'let source_world_to_object=source_graph_inverse_affine(world_m*packet.target_world_from_object*source_graph.target_object_from_source_object);' in source
        assert 'output.view_direction_object=normalize(source_graph_linear(source_world_to_object)*output.view_direction_world);' in source
        assert row['required_original_uv_channels']==1 and row['unused_uv1_not_required']
"""+SCRIPT[end:]
SCRIPT=SCRIPT.replace('native_vertex_programs_reopened=6','native_vertex_programs_reopened=2')
SCRIPT=SCRIPT.replace('actual_positive_compositions_reopened=24','actual_positive_compositions_reopened=8')
SCRIPT=SCRIPT.replace('reopened=6, positive=24','reopened=2, positive=8')
exec(compile(SCRIPT,str(BASE),'exec'),{'__name__':'__main__','__file__':str(Path(__file__).resolve())})
