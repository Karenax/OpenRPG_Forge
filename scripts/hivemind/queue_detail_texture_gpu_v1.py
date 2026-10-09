"""Actual GPU sampling of the two measured Detail textures and all 24 mips."""
from pathlib import Path
BASE=Path(__file__).with_name('queue_delta_rocks_firepit_texture_gpu_v1.py')
text=BASE.read_text('utf-8-sig')
for old,new in [
    ('delta-rocks-firepit-texture-gpu-v1','detail-texture-gpu-v1'),
    ('unity-delta-rocks-firepit-texture-storage-v1','detail-native-texture-storage-v1'),
    ("SCRIPT=SCRIPT.replace('18','10').replace('225','120').replace('eighteen','ten')",
     "SCRIPT=SCRIPT.replace('18','2').replace('225','24').replace('eighteen','two')")]:
    assert old in text,old;text=text.replace(old,new)
if __name__=='__main__':
    exec(compile(text,str(BASE),'exec'),dict(__file__=str(Path(__file__).resolve()),__name__='__main__'))
