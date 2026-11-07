"""Retry the terminal API build failure; retain v1006 and use the same source scope."""
from pathlib import Path

recipe_path = Path(__file__).with_name('compare_villa_builtin_normal_gpu_v1006.py')
code = recipe_path.read_text('utf-8')
assert code.count('villa-builtin-normal-gpu-comparison-v1006') == 1
code = code.replace('villa-builtin-normal-gpu-comparison-v1006', 'villa-builtin-normal-gpu-comparison-v1007')
anchor = "diagnosis_path, native_path, native_run_path, Path(__file__).resolve()"
assert code.count(anchor) == 1
code = code.replace(anchor, anchor + ', recipe_path')
exec(compile(code, str(recipe_path)+'#wgpu29-error-scope-api-repair', 'exec'), globals())
