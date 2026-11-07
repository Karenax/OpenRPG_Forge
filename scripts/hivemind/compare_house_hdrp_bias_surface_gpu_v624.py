"""Recompare unchanged WGSL against the source-faithful HDRP macro reference."""
from pathlib import Path
recipe=Path(__file__).with_name('compare_house_implicit_surface_gpu_v622.py')
code=recipe.read_text('utf-8-sig')
replacements={"O=W/'house-implicit-surface-gpu-comparison-v622'":"O=W/'house-hdrp-bias-surface-gpu-comparison-v624'","N=W/'house-implicit-surface-reference-v621'":"N=W/'house-hdrp-bias-surface-reference-v623'",'openrpg-house-implicit-gpu-v622':'openrpg-house-hdrp-bias-gpu-v624','for p in [Path(__file__),tp,mp,':'for p in [Path(__file__),recipe,tp,mp,','Actual generated surface rasterization vs WGSL on controlled planes with native mip chains/implicit derivatives; no lighting, prefab mesh or depth-host qualification':'Actual generated surface with exact HDRP SampleBias macro at explicit global mip bias zero vs unchanged WGSL on controlled planes; no lighting, arbitrary scene bias, prefab mesh or depth-host qualification'}
for old,new in replacements.items():assert old in code,old;code=code.replace(old,new)
exec(compile(code,str(recipe)+'#source-faithful-hdrp-reference','exec'),globals())
