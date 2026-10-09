"""Reopen Detail's eight actual full-material compositions and six scoped refusals."""
from pathlib import Path
BASE=Path(__file__).with_name('reopen_delta_native_lit_fragments_v1.py')

def main():
    text=BASE.read_text('utf-8-sig')
    pairs=[("ROOT = W / 'delta-native-lit-fragments-v1'","ROOT = W / 'detail-native-lit-fragments-v1'"),
        ("W / 'delta-foliage-render-contracts-v1/RESULT.v1.json'","W / 'detail-original-render-contracts-v1/RESULT.v1.json'"),
        ("W / 'delta-native-surface-interfaces-v1/RESULT.v1.json'","W / 'detail-native-surface-interfaces-v1/RESULT.v1.json'"),
        ("len(result['rows']) == 6","len(result['rows']) == 1"),
        ("result['actual_positive_compositions'] == 46","result['actual_positive_compositions'] == 8"),
        ('assert count == 46','assert count == 8'),
        ('native_lit_programs_reopened=6','native_lit_programs_reopened=1'),
        ('reopened=6,','reopened=1,')]
    for a,b in pairs:
        assert text.count(a)==1,a
        text=text.replace(a,b)
    a="            assert 'surface_probe(' not in text and 'qualification_probe(' not in text"
    assert text.count(a)==1
    text=text.replace(a,a+"\n            assert '@binding(120)' in text and 'if mode==2.0' in text\n            assert 'output.native_default_mask_rg=source_native_default_scalars.lanes[0].xy;' in text\n            assert 'output.normal_mode_1=source_graph.normal_decode_modes[0].y;' in text")
    a="    evidence = dict(schema='hivemind.delta-native-lit-fragments-independent-reopen.v1'"
    assert text.count(a)==1
    text=text.replace(a,"    pins[str(BASE)] = sha(BASE)\n"+a)
    exec(compile(text,str(BASE),'exec'),dict(__name__='__main__',__file__=str(Path(__file__).resolve()),BASE=BASE))

if __name__=='__main__':main()
