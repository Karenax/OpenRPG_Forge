"""Compose two full native Lit/prepass candidates from exact measured source surfaces."""
from pathlib import Path
from delta_foliage_rgorag import candidate

BASE=Path(__file__).with_name('build_delta_native_lit_fragments_v1.py')
HELPER=Path(__file__).with_name('delta_foliage_rgorag.py')
SCRIPT=BASE.read_text('utf-8-sig')

def replace(old,new):
    global SCRIPT
    assert SCRIPT.count(old)==1, old
    SCRIPT=SCRIPT.replace(old,new)

replace("ROOT = W / 'delta-native-lit-fragments-v1'", "ROOT = W / 'delta-rocks-firepit-native-lit-v1'")
replace("contract_path = W / 'delta-foliage-render-contracts-v1/RESULT.v1.json'", "contract_path = W / 'delta-rocks-firepit-render-contracts-v1/RESULT.v1.json'")
replace("surface_path = W / 'delta-native-surface-interfaces-v1/RESULT.v1.json'", "surface_path = W / 'delta-rocks-firepit-native-surface-v1/RESULT.v1.json'")
replace("reopened_path = W / 'delta-native-surface-interfaces-v1/INDEPENDENT-REOPEN.v1.json'", "reopened_path = W / 'delta-rocks-firepit-native-surface-v1/INDEPENDENT-REOPEN.v1.json'")
replace("reopened['native_surface_interfaces_reopened'] == 6", "reopened['native_surface_interfaces_reopened'] == 2")
replace("native = path.read_text('utf-8-sig')", "native = candidate(path.read_text('utf-8-sig'))")
replace("    result = dict(schema='hivemind.delta-native-lit-fragments.v1'", """    pins[str(BASE)],pins[str(HELPER)]=sha(BASE),sha(HELPER)
    result = dict(schema='hivemind.delta-native-lit-fragments.v1'""")
replace('requested=6,','requested=2,')
replace('pipeline_state_admission_pending=True))','pipeline_state_admission_pending=True, rgorag_mode2_requires_measured_gpu_and_host_admission=True))')
exec(compile(SCRIPT,str(BASE),'exec'),{'__name__':'__main__','__file__':str(Path(__file__).resolve()),
    'BASE':BASE,'HELPER':HELPER,'candidate':candidate})
