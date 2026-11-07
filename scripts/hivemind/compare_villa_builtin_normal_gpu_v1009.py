"""Use a valid WGSL entry name; the extracted asset functions stay identical."""
from pathlib import Path

recipe_path = Path(__file__).with_name('compare_villa_builtin_normal_gpu_v1006.py')
code = recipe_path.read_text('utf-8')
for old, new in {
    'villa-builtin-normal-gpu-comparison-v1006': 'villa-builtin-normal-gpu-comparison-v1009',
    'fn diagnostic(@builtin(global_invocation_id)': 'fn kernel_main(@builtin(global_invocation_id)',
    'diagnosis_path, native_path, native_run_path, Path(__file__).resolve()':
        'diagnosis_path, native_path, native_run_path, Path(__file__).resolve(), recipe_path',
}.items():
    assert code.count(old) == 1, old
    code = code.replace(old, new)
exec(compile(code, str(recipe_path)+'#valid-wgsl-diagnostic-entry', 'exec'), globals())
