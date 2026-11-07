"""Check active host scale and explicitly reject calls to the legacy uniform helper."""
from pathlib import Path
BASE = Path(__file__).with_name('seal_house_host_interfaces_v659.py')
code = BASE.read_text('utf-8-sig')
old = "        assert 'output.inverse_object_xz_scale=source_graph.source_scale_mip_min.xy;' not in text"
new = """        active = text.split('fn source_graph_inputs_from_host_stage(', 1)[1].split('fn source_graph_vertex_inputs_from_host(', 1)[0]
        assert 'output.inverse_object_xz_scale=source_graph.source_scale_mip_min.xy;' not in active
        assert text.count('source_graph_bind_globals(') == 1, 'Legacy global binding helper must remain uncalled'
        assert 'output.inverse_object_xz_scale=vec2<f32>(length(' in active"""
assert code.count(old) == 1
code = code.replace(old, new).replace('v659', 'v660')
code = code.replace("    rows = []", "    pins[str(BASE)] = sha(BASE)\n    rows = []")
exec(compile(code, str(BASE) + '#active-host-path-check', 'exec'), globals())
