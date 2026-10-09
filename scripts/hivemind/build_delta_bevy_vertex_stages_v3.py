"""New shared vertex stage generation; historical stage snapshots stay intact."""
from pathlib import Path
from source_graph_instance_index_transport_v2 import repair_vertex_instance_transport
BASE=Path(__file__).with_name('build_delta_bevy_vertex_stages_v2.py')
source=BASE.read_text('utf-8-sig')
source=source.replace("ROOT = W / 'delta-vector-bevy-vertex-stages-v2'","ROOT = W / 'delta-vector-bevy-vertex-stages-v3'").replace("schema='hivemind.delta-vector-bevy-vertex-stages.v2'","schema='hivemind.delta-vector-bevy-vertex-stages.v3'")
marker="    stages_path = W / 'delta-vector-native-stages-v1/RESULT.v1.json'";assert source.count(marker)==1
source=source.replace(marker,'    wrapper = repair_vertex_instance_transport(wrapper)\n'+marker)
marker='    pins[str(previous)] = sha(previous)';assert source.count(marker)==1
source=source.replace(marker,marker+"\n    for helper in [BASE,Path(__file__).with_name('source_graph_instance_index_transport_v1.py'),Path(__file__).with_name('source_graph_instance_index_transport_v2.py')]:pins[str(helper)]=sha(helper)\n    previous_validated = W / 'delta-vector-bevy-vertex-stages-v2/RESULT.v1.json'\n    assert read(previous_validated)['native_vertex_programs_composed']==6\n    pins[str(previous_validated)]=sha(previous_validated)")
source=source.replace('renderer_installed=False, gpu_rendered=False,','renderer_installed=False, explicit_entrypoint_instance_transport=True, gpu_rendered=False,')
exec(compile(source,str(BASE),'exec'),globals())
