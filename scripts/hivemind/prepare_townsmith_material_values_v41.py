"""Compare original generated base colour, material scalars and TS normals."""
from pathlib import Path
import subprocess
import sys

S=Path(__file__).parent
source=S/'queue_townsmith_material_values_v41.py'
builder=S/'build_townsmith_native_material_values_v41.py'
batch=S/'run_townsmith_native_material_values_v41.py'
assert not source.exists() and not builder.exists() and not batch.exists()
q=(S/'queue_townsmith_source_graph_probe_v34.py').read_text(encoding='utf-8')
q=q.replace('v34','v41').replace('V34','V41').replace('townsmith-door-source-graph-probe-v41','townsmith-door-source-material-values-v41')
a='if(_UvProbeMode<0.5)return float4(result.NormalTS,1);if(_UvProbeMode<1.5)return result.ProbeBlend1;return result.ProbeBlend2;'
assert q.count(a)==1
q=q.replace(a,'if(_UvProbeMode<0.5)return float4(result.BaseColor,1);if(_UvProbeMode<1.5)return float4(result.Metallic,result.Smoothness,result.Occlusion,1);return float4(result.NormalTS,1);')
compile(q,str(source),'exec');source.write_text(q,encoding='utf-8')
q=(S/'build_townsmith_native_uv1_graph_probe_v38.py').read_text(encoding='utf-8').replace('v38','v41')
q=q.replace('run_townsmith_native_uv1_graph_probe_v41','run_townsmith_native_material_values_v41')
for a,b in [
 ('let normal=vec4<f32>(raw.normal_ts,1.0);','let normal=vec4<f32>(raw.base_color,1.0);'),
 ('raw.probe_blend1','vec4<f32>(raw.metallic,raw.smoothness,raw.occlusion,1.0)'),
 ('raw.probe_blend2','vec4<f32>(raw.normal_ts,1.0)'),
 ("['normal_ts','blend1','blend2']","['base_color','metal_smooth_ao','normal_ts']"),
]:
 assert q.count(a)==1,(a,q.count(a))
 q=q.replace(a,b)
compile(q,str(builder),'exec');builder.write_text(q,encoding='utf-8')
q=(S/'run_townsmith_native_uv1_graph_probe_v38.py').read_text(encoding='utf-8').replace('v38','v41')
compile(q,str(batch),'exec');batch.write_text(q,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(source)],check=True)
subprocess.run([sys.executable,'-B',str(builder)],check=True)
