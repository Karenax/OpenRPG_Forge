"""Compose Detail's original clipped, double-sided, alpha-to-coverage material."""
from pathlib import Path
BASE=Path(__file__).with_name('build_delta_native_lit_fragments_v1.py')

def main():
    text=BASE.read_text('utf-8-sig')
    pairs=[("ROOT = W / 'delta-native-lit-fragments-v1'","ROOT = W / 'detail-native-lit-fragments-v1'"),
        ("contract_path = W / 'delta-foliage-render-contracts-v1/RESULT.v1.json'","contract_path = W / 'detail-original-render-contracts-v1/RESULT.v1.json'"),
        ("surface_path = W / 'delta-native-surface-interfaces-v1/RESULT.v1.json'","surface_path = W / 'detail-native-surface-interfaces-v1/RESULT.v1.json'"),
        ("reopened_path = W / 'delta-native-surface-interfaces-v1/INDEPENDENT-REOPEN.v1.json'","reopened_path = W / 'detail-native-surface-interfaces-v1/INDEPENDENT-REOPEN.v1.json'"),
        ("reopened['native_surface_interfaces_reopened'] == 6","reopened['native_surface_interfaces_reopened'] == 1"),
        ('requested=6,','requested=1,'),
        ('pipeline_state_admission_pending=True))','pipeline_state_admission_pending=True, measured_default_binding_120_preserved=True, rgorag_mode2_requires_host_admission=True))')]
    for a,b in pairs:
        assert text.count(a)==1,a
        text=text.replace(a,b)
    a="            native = path.read_text('utf-8-sig')"
    assert text.count(a)==1
    text=text.replace(a,a+"\n            assert '@binding(120)' in native and 'if mode==2.0' in native\n            assert 'output.native_default_mask_rg=source_native_default_scalars.lanes[0].xy;' in native")
    a="    result = dict(schema='hivemind.delta-native-lit-fragments.v1'"
    assert text.count(a)==1
    text=text.replace(a,"    pins[str(BASE)] = sha(BASE)\n"+a)
    exec(compile(text,str(BASE),'exec'),dict(__name__='__main__',__file__=str(Path(__file__).resolve()),BASE=BASE))

if __name__=='__main__':main()
