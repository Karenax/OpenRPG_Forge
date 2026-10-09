"""Two UV0 programs use the same explicit instance transport recipe."""
from pathlib import Path
from source_graph_instance_index_transport_v2 import repair_vertex_instance_transport
BASE_RECIPE=Path(__file__).with_name('build_delta_rocks_firepit_bevy_vertex_v1.py')
source=BASE_RECIPE.read_text('utf-8-sig').replace('delta-rocks-firepit-bevy-vertex-v1','delta-rocks-firepit-bevy-vertex-v2')
marker="exec(compile(SCRIPT,str(BASE),'exec'),";assert source.count(marker)==1
injection="SCRIPT=SCRIPT.replace(\"    stages_path = W / 'delta-rocks-firepit-native-stages-v1/RESULT.v1.json'\",\"    wrapper = repair_vertex_instance_transport(wrapper)\\n    stages_path = W / 'delta-rocks-firepit-native-stages-v1/RESULT.v1.json'\")\nSCRIPT=SCRIPT.replace(\"    pins[str(BASE)] = sha(BASE)\",\"    pins[str(BASE)] = sha(BASE)\\n    for helper in [Path(__file__).with_name('source_graph_instance_index_transport_v1.py'),Path(__file__).with_name('source_graph_instance_index_transport_v2.py')]:pins[str(helper)]=sha(helper)\")\nSCRIPT=SCRIPT.replace('renderer_installed=False, gpu_rendered=False,','renderer_installed=False, explicit_entrypoint_instance_transport=True, gpu_rendered=False,')\nSCRIPT=SCRIPT.replace('hivemind.delta-vector-bevy-vertex-stages.v2','hivemind.delta-rocks-firepit-bevy-vertex-stages.v2')\n"
source=source.replace(marker,injection+marker)
source=source.replace("'BASE':BASE}","'BASE':BASE,'repair_vertex_instance_transport':repair_vertex_instance_transport}")
exec(compile(source,str(BASE_RECIPE),'exec'),globals())
