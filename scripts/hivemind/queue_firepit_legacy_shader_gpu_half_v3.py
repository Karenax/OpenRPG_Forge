"""Measure the original shader in ARGBHalf, matching Forge's HDR attachment.

Retain the ARGBFloat measurements and the DX12 FLOAT32_BLENDABLE refusal.
Fixtures, shader, blend, channels, tint and tolerance are not changed.
"""
from pathlib import Path

HERE = Path(__file__).resolve().parent
BASE = HERE / 'queue_firepit_legacy_shader_gpu_builtin_v2.py'
script = BASE.read_text('utf-8')
old = "probe.ROOT = probe.W / 'unity-delta-firepit-legacy-shader-gpu-builtin-v2'"
assert script.count(old) == 1
script = script.replace(old, "probe.ROOT = probe.W / 'unity-delta-firepit-legacy-shader-gpu-half-v3'")
script = script.replace('LegacyParticleShaderGpuProbeBuiltin', 'LegacyParticleShaderGpuProbeBuiltinHalf')
anchor = '    return text\n'
assert script.count(anchor) == 1
script = script.replace(anchor, '''    assert text.count('RenderTextureFormat.ARGBFloat') == 2
    text = text.replace('RenderTextureFormat.ARGBFloat', 'RenderTextureFormat.ARGBHalf')
    text = replace_once(text, 'public class Report {', 'public class Report {public string render_target_format="ARGBHalf";')
    return text
''')
anchor = "    assert report['pipeline_settings_restored']"
assert script.count(anchor) == 1
script = script.replace(anchor, anchor + "\n    assert report['render_target_format'] == 'ARGBHalf'")
exec(compile(script, str(BASE), 'exec'), dict(__name__='__main__', __file__=str(Path(__file__).resolve())))
