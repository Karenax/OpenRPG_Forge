"""Compose exact nine-output surfaces and UV0-only Bevy interfaces for two graphs."""
from pathlib import Path

BASE=Path(__file__).with_name('build_delta_native_surface_interfaces_v1.py')
SCRIPT=BASE.read_text('utf-8-sig')

def replace(old,new):
    global SCRIPT
    assert SCRIPT.count(old)==1, old
    SCRIPT=SCRIPT.replace(old,new)

replace("ROOT = W / 'delta-native-surface-interfaces-v1'", "ROOT = W / 'delta-rocks-firepit-native-surface-v1'")
replace("parent_path = W / 'delta-vector-bevy-vertex-stages-v2/RESULT.v1.json'", "parent_path = W / 'delta-rocks-firepit-bevy-vertex-v1/RESULT.v1.json'")
replace("assert parent['native_vertex_programs_composed'] == 6 and not parent['failures']", "assert parent['native_vertex_programs_composed'] == 2 and not parent['failures']")
replace('assert len(surface_terminals) == 10','assert len(surface_terminals) == 9')
replace('assert len(arguments) == len(fields) == 13','assert len(arguments) == len(fields) == 12')
replace("    topology_path = W / 'nested-prefab-delta-material-active-ir-v1/RESULT.v1.json'", """    # The original targets disable shadow-specific clipping and both graphs have nine surface terminals.
    contract_path = W / 'delta-rocks-firepit-render-contracts-v1/RESULT.v1.json'
    contract=read(contract_path)
    assert contract['contracts_reopened']==2
    for row in contract['rows']:
        builtin=next(r for r in row['original_active_data'] if r['m_Type'].endswith('.BuiltinData'))
        assert not builtin['m_AlphaTestShadow'] and not row['source_keyword_alpha_test']
    declarations=declarations.replace('    @location(9) source_uv1:vec4<f32>,','')
    declarations=declarations.replace('    @location(11) source_uv1:vec4<f32>,','')
    bridge=bridge.replace(' packet.source_uv1=in.source_uv1;','')
    topology_path = W / 'nested-prefab-delta-material-active-ir-v1/RESULT.v1.json'""")
replace("    vertex_old = read(W / 'native-hdrp-nine-source-programs-v1/source-graph-bevy-vertex-wrapper-v4/RESULT.v1.json')", """    pins[str(BASE)]=sha(BASE)
    pins[str(contract_path)]=sha(contract_path)
    for p,h in contract['input_pins'].items():
        assert sha(p)==h
        pins[p]=h
    vertex_old = read(W / 'native-hdrp-nine-source-programs-v1/source-graph-bevy-vertex-wrapper-v4/RESULT.v1.json')""")
replace('            native = case / \'native_vertex_and_surface_functions.wgsl\'', """            wrapper=wrapper.replace(' out.source_uv1=vertex.source_uv1;','')
            wrapper=wrapper.replace(' out.source_uv1=frame.source_uv1;','')
            assert 'source_uv1' not in prefix+declarations+wrapper+bridge
            native = case / 'native_vertex_and_surface_functions.wgsl'""")
replace('raw_source_uv0_and_uv1_interpolated=True,','raw_source_uv0_interpolated=True, unused_uv1_not_required=True,')
replace('requested=6,','requested=2,')
exec(compile(SCRIPT,str(BASE),'exec'),{'__name__':'__main__','__file__':str(Path(__file__).resolve()),'BASE':BASE})
