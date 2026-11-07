"""Compare active L1 diagnostic taps with the original WGSL calculations."""
from pathlib import Path
base_recipe=Path(__file__).with_name('compare_house_sampling_diagnostic_v626.py')
text=base_recipe.read_text('utf-8-sig').replace('house-sampling-diagnostic-comparison-v626','house-active-sampling-diagnostic-comparison-v629').replace('house-sampling-diagnostic-v625','house-active-sampling-diagnostic-v628').replace('openrpg-house-sampling-diagnostic-v626','openrpg-house-active-sampling-diagnostic-v629').replace('for p in [Path(__file__),recipe,tp,mp,','for p in [Path(__file__),base_recipe,recipe,tp,mp,')
exec(compile(text,str(base_recipe)+'#active-L1-taps','exec'),globals())
