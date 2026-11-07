"""Compose actual Bevy host inputs and per-instance POM inverse object scale."""
from pathlib import Path
from source_graph_instance_index_transport_v2 import repair_vertex_instance_transport
S=Path(__file__).resolve().parent;BASE=S/'build_delta_bevy_vertex_stages_v2.py'
code=BASE.read_text('utf-8-sig')
for old,new in {
 'delta-vector-bevy-vertex-stages-v2':'house-bevy-vertex-stages-v656',
 'delta-vector-native-stages-v1/RESULT.v1.json':'house-native-stages-v655/RESULT.v1.json',
 'delta-vector-node-wgsl-v3/RESULT.v1.json':'house-native-bound-surface-candidates-v615/RESULT.v1.json',
 'nested-prefab-delta-material-active-ir-v1/RESULT.v1.json':'house-main-material-lot-v603/RESULT.v1.json',
 "topology = {row['material_guid']: row for row in read(topology_path)['rows']}":"topology = {row['material_guid']:dict(output=row['ir_path'],output_sha256=row['ir_sha256']) for row in read(topology_path)['rows']}",
}.items():assert old in code,old;code=code.replace(old,new)
needle="    stages_path = W / 'house-native-stages-v655/RESULT.v1.json'";assert code.count(needle)==1
addition="""    wrapper=repair_vertex_instance_transport(wrapper)
    import re
    pattern=r'output\\.inverse_object_xz_scale\\s*=\\s*source_graph\\.source_scale_mip_min\\.xy;'
    assert len(re.findall(pattern,coords))==1,'Pinned inherited scale assignment required'
    coords=re.sub(pattern,'',coords)
    anchor='output.source_world_to_object=source_graph_inverse_affine(world_m*packet.target_world_from_object*source_graph.target_object_from_source_object);'
    assert coords.count(anchor)==1
    coords=coords.replace(anchor,anchor+'\\noutput.inverse_object_xz_scale=vec2<f32>(length(vec3<f32>(output.source_world_to_object[0].x,output.source_world_to_object[1].x,output.source_world_to_object[2].x)),length(vec3<f32>(output.source_world_to_object[0].z,output.source_world_to_object[1].z,output.source_world_to_object[2].z)));')
"""+needle
code=code.replace(needle,addition)
needle="    pins[str(previous)] = sha(previous)";assert code.count(needle)==1
code=code.replace(needle,needle+"\n    for helper in [BASE,S/'source_graph_instance_index_transport_v1.py',S/'source_graph_instance_index_transport_v2.py',Path(__file__)]:pins[str(helper)]=sha(helper)\n    geometry_path=W/'house-geometry-checkpoint-v654/RESULT.v1.json';geometry=read(geometry_path);assert geometry['meshes']==42 and geometry['prefab_cards']==24;pins[str(geometry_path)]=sha(geometry_path)")
code=code.replace('renderer_installed=False, gpu_rendered=False,','renderer_installed=False, original_inverse_object_xz_scale_derived_from_instance=True, explicit_entrypoint_instance_transport=True, gpu_rendered=False,')
exec(compile(code,str(BASE)+'#house-native-instance-frame','exec'),globals())
