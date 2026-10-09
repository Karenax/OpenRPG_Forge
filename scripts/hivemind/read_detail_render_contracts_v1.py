"""Read Detail's original target and material state; no renderer qualification."""
from pathlib import Path

BASE=Path(__file__).with_name('read_delta_foliage_render_contracts_v1.py')

def once(t,a,b):
    assert t.count(a)==1,a
    return t.replace(a,b)

def main():
    text=BASE.read_text('utf-8-sig')
    text=once(text,"ROOT = W / 'delta-foliage-render-contracts-v1'","ROOT = W / 'detail-original-render-contracts-v1'")
    text=once(text,"surface_path = W / 'delta-native-surface-interfaces-v1/INDEPENDENT-REOPEN.v1.json'",
        "surface_path = W / 'detail-normal-mode2-bevy-candidate-v1/RESULT.v1.json'")
    text=once(text,"wanted = {row['material_guid'] for row in surfaces['rows']}","wanted = {surfaces['material_guid']}")
    text=once(text,"assert len(wanted) == surfaces['native_surface_interfaces_reopened'] == 6",
        "assert len(wanted)==1 and surfaces['status']=='candidate_composed'")
    text=once(text,"    rows = []\n", "    pins[str(BASE)] = sha(BASE)\n    rows = []\n")
    exec(compile(text,str(BASE),'exec'),dict(__name__='__main__',__file__=str(Path(__file__).resolve()),BASE=BASE))

if __name__=='__main__':main()
