"""Compare four measured native normal images with all ten Forge GPU images."""
from pathlib import Path
BASE=Path('F:/OpenRPG_ImportWork/source-graph-imported-normal-gpu-parity-v2/compare.py')
OUTPUT=Path('F:/OpenRPG_ImportWork/delta-rocks-firepit-normal-raw-gpu-parity-v1')
assert not OUTPUT.exists()
SCRIPT=BASE.read_text('utf-8-sig')
SCRIPT=SCRIPT.replace('ROOT=Path(__file__).resolve().parent',"ROOT=Path('F:/OpenRPG_ImportWork/delta-rocks-firepit-normal-raw-gpu-parity-v1')")
SCRIPT=SCRIPT.replace("'unity-villa-normal-sampling-reference-v1'","'unity-delta-rocks-firepit-normal-reference-v1'")
SCRIPT=SCRIPT.replace("'source-graph-imported-texture-gpu-probe-v2'","'delta-rocks-firepit-texture-gpu-v1'")
SCRIPT=SCRIPT.replace("'normal_sampling_reference_verified'","'four_native_rocks_firepit_normal_references_reopened'")
SCRIPT=SCRIPT.replace("'gpu_44_texture_sampling_buffers_verified'","'ten_textures_and_120_mips_gpu_sampled_and_reopened'")
SCRIPT=SCRIPT.replace('==14','==4').replace('==44','==10').replace('==528','==120')
SCRIPT=SCRIPT.replace('normal_textures_compared=14','normal_textures_compared=4')
SCRIPT=SCRIPT.replace('all44_unity_sampling_parity_qualified','all10_unity_sampling_parity_qualified')
needle=' assert all(sha(path)==digest for path,digest in pins.items())'
assert SCRIPT.count(needle)==1
SCRIPT=SCRIPT.replace(needle," pins[str(BASE)]=sha(BASE)\n"+needle)
OUTPUT.mkdir()
exec(compile(SCRIPT,str(BASE),'exec'),{'__name__':'__main__','__file__':str(Path(__file__).resolve()),'BASE':BASE})
