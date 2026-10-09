"""Reuse exact helper-license preservation for the new Detail shader artifacts."""
from pathlib import Path

if __name__=='__main__':
    base=Path(__file__).with_name('preserve_delta_rocks_firepit_program_licenses_v1.py')
    text=base.read_text('utf-8-sig')
    old="ROOT=W/'delta-rocks-firepit-program-licenses-v1'"
    assert text.count(old)==1
    text=text.replace(old,"ROOT=W/'detail-shader-program-licenses-v1'")
    old="destinations=[W/name for name in ['delta-rocks-firepit-native-stages-v1','delta-rocks-firepit-bevy-vertex-v1',\n        'delta-rocks-firepit-native-surface-v1','delta-rocks-firepit-native-lit-v1',\n        'delta-rocks-firepit-normal-decoder-gpu-v1']]"
    assert text.count(old)==1
    text=text.replace(old,"destinations=[W/name for name in ['detail-native-default-kernel-v1','detail-native-default-stages-v1','detail-native-default-stages-negative-controls-v1']]")
    text=text.replace('pins={str(p):sha(p) for p in [Path(__file__),kernel,pom]}',
        'pins={str(p):sha(p) for p in [Path(__file__),base,kernel,pom]}')
    exec(compile(text,str(Path(__file__).resolve()),'exec'),dict(__file__=str(Path(__file__).resolve()),__name__='__main__',base=base))
