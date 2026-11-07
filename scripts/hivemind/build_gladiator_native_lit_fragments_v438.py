"""Compose full Gladiator Lit fragments using pinned native source contracts."""
from pathlib import Path

S = Path(__file__).resolve().parent
BASE = S / 'build_delta_native_lit_fragments_v1.py'
code = BASE.read_text('utf-8')
edits = {
    'delta-native-lit-fragments-v1': 'gladiator-native-lit-fragments-v438',
    'delta-foliage-render-contracts-v1/RESULT.v1.json': 'gladiator-render-contracts-v432/RESULT.v1.json',
    'delta-native-surface-interfaces-v1/RESULT.v1.json': 'gladiator-native-surface-interfaces-v436/RESULT.v1.json',
    'delta-native-surface-interfaces-v1/INDEPENDENT-REOPEN.v1.json': 'gladiator-stage-composition-delivery-v437/RESULT.v1.json',
    "contracts['contracts_reopened']": "len(contracts['rows'])",
    "    rows, failures = [], []": "    pins[str(BASE)] = sha(BASE)\n    rows, failures = [], []",
    "                if name == 'normal_depth_coverage' and not contract['source_keyword_alpha_to_mask']:":
        "                if name == 'normal_depth_coverage' and not depth_alpha_to_mask:",
    "original_alpha_to_mask=contract['source_keyword_alpha_to_mask'],":
        "original_alpha_to_mask=depth_alpha_to_mask, original_alpha_to_mask_forward=False, original_alpha_to_mask_shadow=False, source_keyword_alpha_to_mask_preserved=contract['source_keyword_alpha_to_mask'],",
}
for old, new in edits.items():
    assert old in code, old
    code = code.replace(old, new)
anchor = "            builtin = next(obj for obj in contract['original_active_data'] if obj['m_Type'].endswith('.BuiltinData'))"
assert code.count(anchor) == 1
code = code.replace(anchor, anchor + '''
            # Resolve the actual generated render state rather than stale keywords.
            passes = {entry['name']: entry['state'] for entry in contract['generated_pass_states']}
            assert 'AlphaToMask' not in passes['Forward']
            assert 'AlphaToMask' not in passes['ShadowCaster']
            assert passes['DepthOnly']['AlphaToMask'] == '[_AlphaCutoffEnable]'
            depth_alpha_to_mask = bool(contract['native_resolved_state']['_AlphaCutoffEnable'])
            assert not builtin['m_DepthOffset'] and not builtin['m_AlphaTestShadow']
            # The source has no DepthOffset terminal; the existing native Lit
            # adapter uses exactly zero offset for this source-disabled feature.
            assert 'out.position=source_graph_depth_position(in,0.0);' in builder
            assert 'depth_offset:' not in native
''')
code = code.replace("        renderer_installed=False, gpu_rendered=False, full_import=False)",
    "        renderer_installed=False, gpu_rendered=False, full_import=False, complete_asset_conversion_increment=0, pass_dependent_alpha_to_mask_preserved=True)")
exec(compile(code, str(BASE) + '#gladiator-native-lit-v438', 'exec'), globals())
