"""Compare raw source sampling for both Detail images, without decoding colour."""
from pathlib import Path
BASE=Path(__file__).with_name('compare_delta_rocks_firepit_raw_gpu_v1.py')
text=BASE.read_text('utf-8-sig')
for old,new in [
    ('delta-rocks-firepit-normal-raw-gpu-parity-v1','detail-source-raw-gpu-parity-v1'),
    ('unity-delta-rocks-firepit-normal-reference-v1','detail-native-sampling-reference-v1'),
    ('delta-rocks-firepit-texture-gpu-v1','detail-texture-gpu-v1'),
    ('four_native_rocks_firepit_normal_references_reopened','two_native_detail_source_normal_references_reopened'),
    ('ten_textures_and_120_mips_gpu_sampled_and_reopened','two_textures_and_24_mips_gpu_sampled_and_reopened'),
    (".replace('==14','==4').replace('==44','==10').replace('==528','==120')",
     ".replace('==14','==2').replace('==44','==2').replace('==528','==24')"),
    ('normal_textures_compared=4','normal_textures_compared=2')]:
    assert old in text,old;text=text.replace(old,new)
if __name__=='__main__':
    exec(compile(text,str(BASE),'exec'),dict(__file__=str(Path(__file__).resolve()),__name__='__main__'))
