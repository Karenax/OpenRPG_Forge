"""Compose House Lit candidates carrying original POM depth to both passes."""
from pathlib import Path
S = Path(__file__).resolve().parent
BASE = S / 'build_delta_native_lit_fragments_v1.py'
code = BASE.read_text('utf-8-sig')
edits = {
    'delta-native-lit-fragments-v1': 'house-native-lit-fragments-v662',
    'delta-foliage-render-contracts-v1/RESULT.v1.json': 'house-render-contracts-v661/RESULT.v1.json',
    'delta-native-surface-interfaces-v1/RESULT.v1.json': 'house-native-surface-interfaces-v658/RESULT.v1.json',
    'delta-native-surface-interfaces-v1/INDEPENDENT-REOPEN.v1.json': 'house-host-interface-checkpoint-v660/RESULT.v1.json',
    "reopened['native_surface_interfaces_reopened']": "reopened['material_count']",
    "assert not contract['source_depth_offset_enabled'] and not contract['source_keyword_transparent']": "assert contract['source_depth_offset_enabled'] and not contract['source_keyword_transparent']\n            assert '_DEPTHOFFSET_ON' in contract['source_valid_keywords']",
    "    rows, failures = [], []": "    pins[str(BASE)] = sha(BASE)\n    rows, failures = [], []",
}
for old, new in edits.items():
    assert old in code, old
    code = code.replace(old, new)
anchor = "    validator = H / 'source-graph-real-lighting-composer-v2/validate_real_bevy_closure.exe'"
addition = '''    old_depth = 'out.position=source_graph_depth_position(in,0.0);'
    assert lit_builder.count(old_depth) == 1
    lit_builder = lit_builder.replace(old_depth, 'out.position=source_graph_depth_position(in,surface.depth_offset);')
    declaration = ' @location(0) color:vec4<f32>,'
    assert forward_tail.count(declaration) == 1
    forward_tail = forward_tail.replace(declaration, declaration + '\\n @builtin(frag_depth) frag_depth:f32,')
    anchor_depth = ' p.frag_coord=in.position;'
    assert forward_tail.count(anchor_depth) == 1
    forward_tail = forward_tail.replace(anchor_depth, anchor_depth + '\\n p.frag_coord.z=surface.position.device_depth;\\n out.frag_depth=surface.position.device_depth;')
    old_prepass = '    out.frag_depth=\\n#ifdef UNCLIPPED_DEPTH_ORTHO_EMULATION\\nin.unclipped_depth\\n#else\\nin.position.z\\n#endif\\n;'
    assert prepass_tail.count(old_prepass) == 1
    prepass_tail = prepass_tail.replace(old_prepass, '    out.frag_depth=surface.position.device_depth;')
'''
assert code.count(anchor) == 1
code = code.replace(anchor, addition + anchor)
code = code.replace('native_camera_exposure_policy=True,', 'native_camera_exposure_policy=True, original_pom_offset_carried_to_forward_and_prepass=True, orthographic_depth_raster_parity_pending=True,')
exec(compile(code, str(BASE) + '#house-original-depth-lit', 'exec'), globals())
