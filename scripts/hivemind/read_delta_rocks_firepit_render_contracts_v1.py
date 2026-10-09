"""Reopen the original active HDRP target and material contract for two source graphs."""
from pathlib import Path

BASE=Path(__file__).with_name('read_delta_foliage_render_contracts_v1.py')
SCRIPT=BASE.read_text('utf-8-sig')

def replace(old,new):
    global SCRIPT
    assert SCRIPT.count(old)==1, old
    SCRIPT=SCRIPT.replace(old,new)

replace("ROOT = W / 'delta-foliage-render-contracts-v1'", "ROOT = W / 'delta-rocks-firepit-render-contracts-v1'")
replace("surface_path = W / 'delta-native-surface-interfaces-v1/INDEPENDENT-REOPEN.v1.json'",
        "surface_path = W / 'delta-rocks-firepit-effective-state-v1/RESULT.v1.json'")
replace("assert len(wanted) == surfaces['native_surface_interfaces_reopened'] == 6",
        "assert len(wanted) == surfaces['materials_compared'] == 2")
exec(compile(SCRIPT,str(BASE),'exec'),{'__name__':'__main__','__file__':str(Path(__file__).resolve())})
