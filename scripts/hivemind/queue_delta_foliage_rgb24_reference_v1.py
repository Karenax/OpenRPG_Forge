"""Measure three original RGB24 mask textures; keep normal diagnostics unqualified."""
from pathlib import Path
import json

BASE = Path(__file__).with_name('queue_delta_foliage_normal_reference_v1.py')
W = Path('F:/OpenRPG_ImportWork')
RGB_GUIDS = {row['actual_guid'] for row in json.loads((W / 'unity-delta-foliage-texture-storage-v2/export/RESULT.json').read_text('utf-8-sig'))['cases']
             if row['graphics_format'] == 'R8G8B8_UNorm'}
assert len(RGB_GUIDS) == 3
script = BASE.read_text('utf-8-sig')
script = script.replace("ROOT = W / 'unity-delta-foliage-normal-reference-v1'", "ROOT = W / 'unity-delta-foliage-rgb24-reference-v1'")
script = script.replace("if texture['requires_normal_decode']", "if texture['guid'] in RGB_GUIDS")
script = script.replace('len(guids) == 6', 'len(guids) == 3').replace('len(cases) == 6', 'len(cases) == 3')
script = script.replace('DeltaFoliageNormalSamplingSnapshotV1.cs', 'DeltaFoliageRgb24SamplingSnapshotV1.cs')
script = script.replace('OpenRPGDeltaFoliageNormalSamplingProbe', 'OpenRPGDeltaFoliageRgb24SamplingProbe')
script = script.replace('assert not helper.exists() and not shader.exists()', "assert not helper.exists() and shader.is_file()\n    assert sha(shader) == sha(OLD / 'NormalSamplingReference.shader')")
script = script.replace('shader.parent.mkdir()', 'shader.parent.mkdir(exist_ok=True)')
script = script.replace("shutil.copyfile(OLD / 'NormalSamplingReference.shader', shader)", "assert sha(shader) == sha(OLD / 'NormalSamplingReference.shader')")
script = script.replace("verifier.replace('==14', '==6').replace(\"'verified_textures':14\", \"'verified_textures':6\")",
                        "verifier.replace('==14', '==3').replace(\"'verified_textures':14\", \"'verified_textures':3\")")
script = script.replace('dict(cases=6,', 'dict(cases=3,').replace('normal_textures=6', 'rgb24_textures=3')
script = script.replace("report['verified_textures'] == 6", "report['verified_textures'] == 3")
script = script.replace('native_normal_textures_verified=6', 'native_rgb24_textures_verified=3')
script = script.replace('native_normal_textures_verified=0', 'native_rgb24_textures_verified=0')
script = script.replace('capturing_six_exact_foliage_normal_references', 'capturing_three_original_rgb24_sampling_references')
script = script.replace('six_native_foliage_normal_references_reopened', 'three_original_rgb24_sampling_references_reopened')
needle = '    check(pins)\n    ROOT.mkdir()'
assert script.count(needle) == 1
script = script.replace(needle, '    pins[str(BASE)] = sha(BASE)\n' + needle)
exec(compile(script, str(BASE), 'exec'))
