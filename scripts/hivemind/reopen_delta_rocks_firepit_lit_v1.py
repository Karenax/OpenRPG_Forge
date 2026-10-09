"""Reopen fourteen actual lighting/prepass compositions and six scoped refusals."""
from pathlib import Path
from delta_foliage_rgorag import candidate,CANDIDATE

BASE=Path(__file__).with_name('reopen_delta_native_lit_fragments_v1.py')
HELPER=Path(__file__).with_name('delta_foliage_rgorag.py')
SCRIPT=BASE.read_text('utf-8-sig')

def replace(old,new):
    global SCRIPT
    assert SCRIPT.count(old)==1, old
    SCRIPT=SCRIPT.replace(old,new)

replace("ROOT = W / 'delta-native-lit-fragments-v1'", "ROOT = W / 'delta-rocks-firepit-native-lit-v1'")
SCRIPT=SCRIPT.replace("W / 'delta-foliage-render-contracts-v1/RESULT.v1.json'", "W / 'delta-rocks-firepit-render-contracts-v1/RESULT.v1.json'")
SCRIPT=SCRIPT.replace("W / 'delta-native-surface-interfaces-v1/RESULT.v1.json'", "W / 'delta-rocks-firepit-native-surface-v1/RESULT.v1.json'")
replace("assert text.startswith(Path(interfaces[guid]['native']).read_text('utf-8-sig'))", """assert text.startswith(candidate(Path(interfaces[guid]['native']).read_text('utf-8-sig')))
            assert text.count(CANDIDATE)==1
            import re
            slots=re.findall(r'normal_mode_(\\d+):f32',text)
            assert len(slots)==(1 if guid=='abc00000000009294800760372203426' else 3)
            for slot in slots:
                assert 'i.normal_mode_'+slot in text
                assert f'source_graph.normal_decode_modes[{int(slot)//4}].{"xyzw"[int(slot)%4]}' in text""")
replace("    evidence = dict(schema='hivemind.delta-native-lit-fragments-independent-reopen.v1'", """    pins[str(BASE)],pins[str(HELPER)]=sha(BASE),sha(HELPER)
    evidence = dict(schema='hivemind.delta-native-lit-fragments-independent-reopen.v1'""")
SCRIPT=SCRIPT.replace('== 6','== 2').replace('== 46','== 14')
SCRIPT=SCRIPT.replace('native_lit_programs_reopened=6','native_lit_programs_reopened=2')
SCRIPT=SCRIPT.replace('reopened=6,','reopened=2,')
exec(compile(SCRIPT,str(BASE),'exec'),{'__name__':'__main__','__file__':str(Path(__file__).resolve()),
    'BASE':BASE,'HELPER':HELPER,'candidate':candidate,'CANDIDATE':CANDIDATE})
