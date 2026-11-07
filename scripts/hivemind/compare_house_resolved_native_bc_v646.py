"""Verify exact native BC resources under the API-confirmed source quality policy."""
from pathlib import Path
_bc_recipe=Path(__file__).with_name('compare_house_native_bc_sampling_v642.py')
exec(compile(_bc_recipe.read_text('utf-8-sig').rsplit('\nexec(compile(code,',1)[0],str(_bc_recipe)+'#prepare-recipe','exec'),globals())
code=code.replace('house-native-bc-sampling-comparison-v642','house-resolved-native-bc-comparison-v646').replace('openrpg-house-native-bc-sampling-v642','openrpg-house-resolved-native-bc-v646')
needle="(O/'src/main.rs').write_text(renderer,'utf-8')"
addition="""a='mag_filter:wgpu::FilterMode::Linear,lod_min_clamp:';assert renderer.count(a)==1;renderer=renderer.replace(a,'mag_filter:wgpu::FilterMode::Linear,mipmap_filter:wgpu::MipmapFilterMode::Linear,anisotropy_clamp:9,lod_min_clamp:').replace('Nearest (source Bilinear)','Linear/aniso 9 (API-confirmed Unity ForceEnable; source Bilinear/aniso 1 preserved)')
"""+needle
assert code.count(needle)==1;code=code.replace(needle,addition)
needle="for p in [BC/'PREPARED.v1.json',BC/'NATIVE-RESULT.v1.json',BC/'RUN-RESULT.v1.json']:pins[str(p)]=sha(p)"
addition=needle+"""
checkpoint=W/'house-resolved-sampling-checkpoint-v645/RESULT.v1.json';quality=read(checkpoint);assert quality['native_quality_mode']=='ForceEnable' and quality['effective_anisotropy']==9 and quality['effective_mip_filter']=='Linear' and quality['implicit_sampling_verified']
for p,h in quality['output_pins'].items():assert sha(p)==h,p;pins[p]=h
for p in [checkpoint,_bc_recipe,W/'house-native-sampling-policy-v644/NATIVE-RESULT.v1.json']:pins[str(p)]=sha(p)
"""
assert code.count(needle)==1;code=code.replace(needle,addition)
code=code.replace('implicit_sampling_verified=failed_total==0','implicit_sampling_verified=failed_total==0,native_bc_sampling_with_quality_verified=failed_total==0,effective_anisotropy=9,effective_mip_filter="Linear"')
code=code.replace('Actual generated surface rasterization vs unchanged WGSL with original native BC mip blocks and implicit derivatives; controlled planes, zero global bias; no depth/prefab/Studio qualification','Actual generated surface vs unchanged WGSL with original BC blocks and API-confirmed ForceEnable sampling (anisotropy 9/linear mips); controlled planes, zero global bias; no depth/prefab/Studio qualification')
exec(compile(code,str(_bc_recipe)+'#resolved-native-BC','exec'),globals())
