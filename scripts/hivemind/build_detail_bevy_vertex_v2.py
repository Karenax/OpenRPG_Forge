"""Use the measured AOV binding with the preserved Bevy ABI adapter."""
from pathlib import Path
if __name__=='__main__':
    base=Path(__file__).with_name('build_detail_bevy_vertex_v1.py')
    text=base.read_text('utf-8-sig')
    for old,new in [('detail-default-native-rg-binding-v1','detail-default-native-rg-binding-v2'),
                    ('detail-native-default-bevy-vertex-v1','detail-native-default-bevy-vertex-v2'),
                    ('detail-native-default-bevy-compositions-v1','detail-native-default-bevy-compositions-v2')]:
        assert old in text;text=text.replace(old,new)
    exec(compile(text,str(base),'exec'),dict(__file__=str(Path(__file__).resolve()),__name__='__main__'))
