"""Reopen Detail's ten-output closure with the independent surface verifier."""
from pathlib import Path
BASE=Path(__file__).with_name('reopen_delta_native_surface_interfaces_v1.py')

def main():
    text=BASE.read_text('utf-8-sig')
    pairs=[("ROOT = W / 'delta-native-surface-interfaces-v1'","ROOT = W / 'detail-native-surface-interfaces-v1'"),
        ("W / 'delta-vector-bevy-vertex-stages-v2/RESULT.v1.json'","W / 'detail-native-surface-selected-parent-v1/RESULT.v1.json'"),
        ("result['native_surface_interfaces_composed'] == 6","result['native_surface_interfaces_composed'] == 1"),
        ("result['actual_positive_compositions'] == 24","result['actual_positive_compositions'] == 4"),
        ('native_surface_interfaces_reopened=6','native_surface_interfaces_reopened=1'),
        ('actual_positive_compositions_reopened=24','actual_positive_compositions_reopened=4'),
        ('reopened=6, compositions=24','reopened=1, compositions=4')]
    for a,b in pairs:
        assert text.count(a)==1,a
        text=text.replace(a,b)
    text=text.replace("    pins[str(receipt)], pins[str(Path(__file__).resolve())] = sha(receipt), sha(__file__)",
        "    pins[str(receipt)], pins[str(Path(__file__).resolve())] = sha(receipt), sha(__file__)\n    pins[str(BASE)] = sha(BASE)")
    exec(compile(text,str(BASE),'exec'),dict(__name__='__main__',__file__=str(Path(__file__).resolve()),BASE=BASE))

if __name__=='__main__':main()
