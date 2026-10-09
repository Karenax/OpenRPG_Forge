"""Change only private diagnostic forward output; textures overridden by harness."""
from pathlib import Path

def mip_diagnostic(text):
    if ' let surface_mode=(u32(in.position.x)' not in text or 'out.color=vec4<f32>(surface_value,0.25);' in text:
        return text
    start=text.index(' let surface_mode=(u32(in.position.x)')
    end=text.index(' return out;',start)
    return text[:start]+''' let mip_palette=textureSampleBias(t0,s0,in.source_uv0.xy,source_graph.source_scale_mip_min.z);
 out.color=vec4<f32>(mip_palette.rgb,0.5);
'''+text[end:]

BASE=Path(__file__).with_name('prepare_firepit_gpu_frame_instance_fix_v1.py')
source=BASE.read_text('utf-8').replace('from source_graph_instance_index_transport_v1 import repair_vertex_instance_transport','')
source=source.replace("OLD=W/'firepit-gpu-frame-matrix-case-v1'","OLD=W/'firepit-surface-instance-fix-case-v1'")
source=source.replace('firepit-gpu-frame-instance-fix-case-v1','firepit-mip-palette-native-case-v1')
source=source.replace('private-instance-transport.glb','private-mip-palette-diagnostic.glb')
source=source.replace('explicit_entrypoint_instance_transport_candidate_prepared','implicit_mip_palette_private_forward_diagnostic_prepared')
repair_vertex_instance_transport=mip_diagnostic
exec(compile(source,str(BASE),'exec'),globals())
