"""Compare exactly six measured foliage images with the 18-image GPU batch."""
from pathlib import Path

BASE = Path('F:/OpenRPG_ImportWork/source-graph-imported-normal-gpu-parity-v2/compare.py')
OUTPUT = Path('F:/OpenRPG_ImportWork/delta-foliage-normal-raw-gpu-parity-v1')
assert not OUTPUT.exists()
script = BASE.read_text('utf-8-sig')
script = script.replace('ROOT=Path(__file__).resolve().parent', "ROOT=Path('F:/OpenRPG_ImportWork/delta-foliage-normal-raw-gpu-parity-v1')")
script = script.replace("'unity-villa-normal-sampling-reference-v1'", "'unity-delta-foliage-normal-reference-v1'")
script = script.replace("'source-graph-imported-texture-gpu-probe-v2'", "'delta-foliage-texture-gpu-v3'")
script = script.replace("'normal_sampling_reference_verified'", "'six_native_foliage_normal_references_reopened'")
script = script.replace("'gpu_44_texture_sampling_buffers_verified'", "'eighteen_textures_and_225_mips_gpu_sampled_and_reopened'")
script = script.replace('==14', '==6').replace('==44', '==18').replace('==528', '==225')
script = script.replace('normal_textures_compared=14', 'normal_textures_compared=6')
needle = ' assert all(sha(path)==digest for path,digest in pins.items())'
assert script.count(needle) == 1
script = script.replace(needle, " pins[str(BASE)]=sha(BASE)\n" + needle)
OUTPUT.mkdir()
exec(compile(script, str(BASE), 'exec'))
