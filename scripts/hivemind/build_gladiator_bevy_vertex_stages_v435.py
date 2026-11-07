"""Compose Bevy wrappers with the existing instance-index transport correction."""
from pathlib import Path
from source_graph_instance_index_transport_v2 import repair_vertex_instance_transport
S=Path(__file__).resolve().parent;BASE=S/'build_delta_bevy_vertex_stages_v2.py'
code=BASE.read_text('utf-8-sig')
edits={
 'delta-vector-bevy-vertex-stages-v2':'gladiator-bevy-vertex-stages-v435',
 'delta-vector-native-stages-v1/RESULT.v1.json':'gladiator-native-stages-v434/RESULT.v1.json',
 'delta-vector-node-wgsl-v3/RESULT.v1.json':'gladiator-material-kernels-v419/RESULT.v1.json',
 'nested-prefab-delta-material-active-ir-v1/RESULT.v1.json':'gladiator-active-material-ir-v386/RESULT.v1.json',
 "topology = {row['material_guid']: row for row in read(topology_path)['rows']}":"topology = {row['guid']:dict(material_guid=row['guid'],output=row['path'],output_sha256=row['sha256']) for row in read(topology_path)['rows']}"
}
for a,b in edits.items():assert a in code,a;code=code.replace(a,b)
needle="    stages_path = W / 'gladiator-native-stages-v434/RESULT.v1.json'";assert code.count(needle)==1
code=code.replace(needle,'    wrapper = repair_vertex_instance_transport(wrapper)\n'+needle)
needle='    pins[str(previous)] = sha(previous)';assert code.count(needle)==1
code=code.replace(needle,needle+"\n    for helper in [BASE,S/'source_graph_instance_index_transport_v1.py',S/'source_graph_instance_index_transport_v2.py']:pins[str(helper)]=sha(helper)\n    first=read(W/'gladiator-native-geometry-v380/NATIVE-RESULT.v1.json')\n    remaining=read(W/'gladiator-remaining-native-geometry-v400/NATIVE-RESULT.v1.json')\n    meshes=first['meshes']+[m for c in remaining['cases'] for m in c['meshes']]\n    assert len(meshes)==17 and all({'TexCoord0','TexCoord1'}.issubset({a['semantic'] for a in m['attributes']}) for m in meshes)\n    for name in ['gladiator-native-geometry-v380/NATIVE-RESULT.v1.json','gladiator-remaining-native-geometry-v400/NATIVE-RESULT.v1.json']:pins[str(W/name)]=sha(W/name)")
code=code.replace('renderer_installed=False, gpu_rendered=False,','renderer_installed=False, explicit_entrypoint_instance_transport=True, original_uv_channels_present_in_all_seventeen_meshes=True, gpu_rendered=False,')
exec(compile(code,str(BASE)+'#gladiator','exec'),globals())
