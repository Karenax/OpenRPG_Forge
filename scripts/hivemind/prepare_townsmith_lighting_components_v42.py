"""Observe actual corrected direct/ambient/reflection terms without pixel fitting."""
from pathlib import Path
import subprocess
import sys

S=Path(__file__).parent
builder=S/'build_townsmith_lighting_components_v42.py'
batch=S/'run_townsmith_lighting_components_v42.py'
assert not builder.exists() and not batch.exists()
q=(S/'build_townsmith_source_uv1_lighting_v37.py').read_text(encoding='utf-8')
for a,b in [
 ('townsmith-source-uv1-lighting-carrier-v37','townsmith-lighting-components-carrier-v42'),
 ('townsmith-native-source-uv1-lighting-v37','townsmith-native-lighting-components-v42'),
 ('verify_source_glb_source_uv1_lighting_v37','verify_source_glb_lighting_components_v42'),
 ('queue_source_glb_source_uv1_lighting_v37','queue_source_glb_lighting_components_v42'),
 ('SM_Door-Single-B.source-uv1-lighting.candidate.glb','SM_Door-Single-B.lighting-components.candidate.glb'),
]:
 assert a in q
 q=q.replace(a,b)
patch='''
assert text.count('source_time_parameters')==1
ambient='indirect_light+=source_scene_irradiance*source_scene_fgd*surface.diffuse_color*surface.diffuse_occlusion;'
assert text.count(ambient)==1
text=text.replace(ambient,'let source_measured_ambient=source_scene_irradiance*source_scene_fgd*surface.diffuse_color*surface.diffuse_occlusion;\\n    indirect_light+=source_measured_ambient;')
reflection='indirect_light+=source_deferred_sky_specular(source_reflection_normal,source_reflection_view,\\n        perceptual_roughness,F0,surface.specular_occlusion,1.0,1.0,1.0);'
assert text.count(reflection)==1
text=text.replace(reflection,'let source_measured_reflection=source_deferred_sky_specular(source_reflection_normal,source_reflection_view,\\n        perceptual_roughness,F0,surface.specular_occlusion,1.0,1.0,1.0);\\n    indirect_light+=source_measured_reflection;')
combined='(transmitted_light + direct_light + indirect_light)'
assert text.count(combined)==1
text=text.replace(combined,'source_private_light_layer'+combined[:-1]+',direct_light,source_measured_ambient,source_measured_reflection,source_graph.source_time_parameters.w)')
text+='\\nfn source_private_light_layer(full:vec3<f32>,direct:vec3<f32>,ambient:vec3<f32>,reflection:vec3<f32>,mode:f32)->vec3<f32> { if(mode<0.5){return full;} if(mode<1.5){return direct;} if(mode<2.5){return ambient;} return reflection; }\\n'
'''
a='text=preserve_source_uv1(text)';assert q.count(a)==1
q=q.replace(a,a+'\n'+patch)
a="EXAMPLE.write_text(example, encoding='utf-8')";assert q.count(a)==1
q=q.replace(a,"assert example.count('source_time_parameters: Vec4::ZERO,')==1\nexample=example.replace('source_time_parameters: Vec4::ZERO,','source_time_parameters: Vec4::new(0.0,0.0,0.0,input[\"diagnostic_layer\"].as_u64().filter(|v| *v < 4).ok_or(\"Lighting layer 0..3 required\")? as f32),')\n"+a)
a="compile(queue, str(QUEUE), 'exec')";assert q.count(a)==1
queue_patch='''
assert queue.count("ROOT=W/'townsmith-native-lighting-components-v42'")==1
queue=queue.replace("ROOT=W/'townsmith-native-lighting-components-v42'","LAYER=int(os.environ['OPENRPG_PRIVATE_LIGHT_LAYER']);assert 0<=LAYER<4\\nROOT=W/f'townsmith-native-lighting-layer-{LAYER}-v42'")
assert queue.count('dict(glb=str(GLB),')==1
queue=queue.replace('dict(glb=str(GLB),','dict(diagnostic_layer=LAYER,glb=str(GLB),')
'''
q=q.replace(a,queue_patch+'\n'+a)
a="subprocess.run([sys.executable, '-B', str(QUEUE)], check=True)";assert q.count(a)==1
q=q.replace(a,"subprocess.run([sys.executable, '-B', str(S/'run_townsmith_lighting_components_v42.py')], check=True)")
compile(q,str(builder),'exec');builder.write_text(q,encoding='utf-8')
q=(S/'run_townsmith_native_uv1_graph_probe_v38.py').read_text(encoding='utf-8')
q=q.replace('townsmith-native-graph-probe-batch-v38','townsmith-lighting-components-batch-v42').replace('townsmith-native-graph-probe-channel-','townsmith-native-lighting-layer-').replace('-v38/','-v42/').replace('queue_source_glb_graph_probe_v38','queue_source_glb_lighting_components_v42').replace('OPENRPG_PRIVATE_UV_CHANNEL','OPENRPG_PRIVATE_LIGHT_LAYER').replace('range(3)','range(4)').replace('sequential_uv_controls_running','sequential_lighting_components_running').replace('three_native_graph_probe_channels_drawn_with_stable_readbacks','four_lighting_components_drawn_with_stable_readbacks')
compile(q,str(batch),'exec');batch.write_text(q,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(builder)],check=True)
