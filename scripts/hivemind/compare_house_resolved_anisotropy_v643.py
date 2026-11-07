"""Apply the existing measured Unity quality resolver; preserve declared settings."""
from pathlib import Path
recipe=Path(__file__).with_name('compare_house_linear_mip_sampling_v640.py')
text=recipe.read_text('utf-8-sig').replace('house-linear-mip-sampling-comparison-v640','house-resolved-anisotropy-comparison-v643').replace('openrpg-house-linear-mip-sampling-v640','openrpg-house-resolved-anisotropy-v643')
prefix=text.rsplit('\nexec(compile(code,',1)[0];exec(compile(prefix,str(recipe)+'#prepare-recipe','exec'),globals())
needle="\npins={}\n"
addition="""import re
from resolve_unity_sampling_policy_v267 import resolve_unity_sampling_policy
quality_path=W/'unity-library-source-thumbnail-house-bench-v2/project/ProjectSettings/QualitySettings.asset'
quality=quality_path.read_text('utf-8-sig');index=int(re.search(r'^  m_CurrentQuality: (\\d+)$',quality,re.M)[1]);settings=[int(x) for x in re.findall(r'^    anisotropicTextures: (\\d+)$',quality,re.M)]
quality_mode=['Disable','Enable','ForceEnable'][settings[index]];assert quality_mode=='ForceEnable'
profiles=read(W/'house-dxc-surface-reference-v638/NATIVE-RESULT.v1.json')['sampler_profiles'];assert len(profiles)==18 and all(p['aniso']==1 and p['filter']=='Bilinear' and p['mip_bias']==0 for p in profiles)
policies=[resolve_unity_sampling_policy(p['aniso'],1,quality_mode,'Direct3D11') for p in profiles];assert all(p['effective_anisotropy']==9 and p['effective_mip_filter']=='Linear' for p in policies)
pins={}\nfor p in [quality_path,S/'resolve_unity_sampling_policy_v267.py',S/'compare_house_linear_mip_sampling_v640.py',W/'house-dxc-surface-reference-v638/NATIVE-RESULT.v1.json']:pins[str(p)]=sha(p)"""
assert code.count(needle)==1;code=code.replace(needle,'\n'+addition+'\n')
a='mipmap_filter:wgpu::MipmapFilterMode::Linear,lod_min_clamp:';assert code.count(a)==1;code=code.replace(a,'mipmap_filter:wgpu::MipmapFilterMode::Linear,anisotropy_clamp:9,lod_min_clamp:')
code=code.replace('Linear (tested candidate, nominal source Bilinear unchanged)','Linear, anisotropy 9 (existing Unity ForceEnable resolver; nominal source Bilinear/aniso 1 unchanged)')
code=code.replace('linear_mip_candidate=True','linear_mip_candidate=True,quality_anisotropy_policy=policies,quality_policy_config_pinned=True,native_quality_api_capture_pending=True')
code=code.replace('Linear mip interpolation candidate against unchanged native source; implicit derivatives and bias zero fixture; no prefab/depth/Studio qualification','Existing Unity ForceEnable quality policy resolved to anisotropy 9/linear mips against unchanged source; config pinned, native quality API capture pending; no prefab/depth/Studio qualification')
exec(compile(code,str(recipe)+'#resolved-quality-anisotropy','exec'),globals())
