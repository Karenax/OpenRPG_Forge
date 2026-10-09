"""Verify original RGB24 vs expanded RGBA8 at all measured native coordinates."""
from pathlib import Path
import hashlib
import json

BASE = Path('F:/OpenRPG_ImportWork/source-graph-imported-normal-gpu-parity-v2/compare.py')
W = Path('F:/OpenRPG_ImportWork')
OUTPUT = W / 'delta-foliage-rgb24-expansion-gpu-parity-v1'
assert not OUTPUT.exists()
native = json.loads((W / 'unity-delta-foliage-rgb24-reference-v1/export/RESULT.json').read_text('utf-8-sig'))
inputs = json.loads((W / 'delta-foliage-texture-gpu-v3/INPUT.v1.json').read_text('utf-8-sig'))
expected = {row['asset_id']: row for row in inputs['rows']}
assert len(native['cases']) == 3
for case in native['cases']:
    row = expected[case['asset_id']]
    assert case['graphics_format'] == 'R8G8B8_UNorm' and row['gpu_format'] == 'Rgba8Unorm' and row['rgb24_expansion']
    original = Path(row['original_storage']).read_bytes()
    derived = Path(row['storage']).read_bytes()
    assert hashlib.sha256(original).hexdigest() == row['original_storage_sha256']
    assert hashlib.sha256(derived).hexdigest() == row['storage_sha256']
    assert len(original) % 3 == 0 and len(derived) == len(original) // 3 * 4
    assert all(derived[c::4] == original[c::3] for c in range(3))
    assert derived[3::4] == b'\xff' * (len(original) // 3)
script = BASE.read_text('utf-8-sig')
script = script.replace('ROOT=Path(__file__).resolve().parent', "ROOT=Path('F:/OpenRPG_ImportWork/delta-foliage-rgb24-expansion-gpu-parity-v1')")
script = script.replace("'unity-villa-normal-sampling-reference-v1'", "'unity-delta-foliage-rgb24-reference-v1'")
script = script.replace("'source-graph-imported-texture-gpu-probe-v2'", "'delta-foliage-texture-gpu-v3'")
script = script.replace("'normal_sampling_reference_verified'", "'three_original_rgb24_sampling_references_reopened'")
script = script.replace("'gpu_44_texture_sampling_buffers_verified'", "'eighteen_textures_and_225_mips_gpu_sampled_and_reopened'")
script = script.replace('==14', '==3').replace('==44', '==18').replace('==528', '==225')
script = script.replace('normal_textures_compared=14', 'rgb24_textures_compared=3')
script = script.replace('normal_texture_raw_sampling_verified=verified', 'rgb24_expansion_raw_sampling_verified=verified')
script = script.replace('normal_decoder_gpu_qualified=False', 'renderer_rgb24_transport_implemented=False')
script = script.replace('all44_unity_sampling_parity_qualified=False', 'all18_unity_sampling_parity_qualified=False')
script = script.replace("schema='openrpg.imported-normal-raw-gpu-parity.v1'", "schema='openrpg.rgb24-expansion-gpu-parity.v1'")
needle = ' assert all(sha(path)==digest for path,digest in pins.items())'
assert script.count(needle) == 1
script = script.replace(needle, " pins[str(BASE)]=sha(BASE)\n" + needle)
OUTPUT.mkdir()
exec(compile(script, str(BASE), 'exec'))
