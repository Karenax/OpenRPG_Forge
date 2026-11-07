"""Measure adjacent-mip interpolation rather than infer it from filter metadata."""
from pathlib import Path
recipe=Path(__file__).with_name('compare_house_implicit_surface_gpu_v622.py')
code=recipe.read_text('utf-8-sig').replace("O=W/'house-implicit-surface-gpu-comparison-v622'","O=W/'house-linear-mip-sampling-comparison-v640'").replace("N=W/'house-implicit-surface-reference-v621'","N=W/'house-hdrp-bias-surface-reference-v623'").replace('openrpg-house-implicit-gpu-v622','openrpg-house-linear-mip-sampling-v640')
needle="(O/'src/main.rs').write_bytes((S/'candidates/house_implicit_surface_gpu_v622.rs').read_bytes())"
new="""renderer=(S/'candidates/house_implicit_surface_gpu_v622.rs').read_text('utf-8-sig');assert renderer.count('mag_filter:wgpu::FilterMode::Linear,lod_min_clamp:')==1;renderer=renderer.replace('mag_filter:wgpu::FilterMode::Linear,lod_min_clamp:','mag_filter:wgpu::FilterMode::Linear,mipmap_filter:wgpu::MipmapFilterMode::Linear,lod_min_clamp:').replace('Nearest (source Bilinear)','Linear (tested candidate, nominal source Bilinear unchanged)');(O/'src/main.rs').write_text(renderer,'utf-8')"""
assert code.count(needle)==1;code=code.replace(needle,new)
code=code.replace('for p in [Path(__file__),tp,mp,','for p in [Path(__file__),recipe,tp,mp,')
code=code.replace('Actual generated surface rasterization vs WGSL on controlled planes with native mip chains/implicit derivatives; no lighting, prefab mesh or depth-host qualification','Linear mip interpolation candidate against unchanged native source; implicit derivatives and bias zero fixture; no prefab/depth/Studio qualification')
code=code.replace('implicit_sampling_verified=failed_total==0','implicit_sampling_verified=failed_total==0,linear_mip_candidate=True')
exec(compile(code,str(recipe)+'#linear-mip-candidate','exec'),globals())
