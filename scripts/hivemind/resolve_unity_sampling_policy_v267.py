"""Resolve measured Unity global anisotropy policy for Direct3D textures.
Source: Unity 6000.0 Texture.anisoLevel; admits only documented/measured inputs.
No asset names, fitted mip bias or modified source filtering declarations.
"""
def resolve_unity_sampling_policy(texture_anisotropy,filter_mode,quality_anisotropy,source_backend):
 if type(texture_anisotropy) is not int or not 0<=texture_anisotropy<=16:raise ValueError('Unsupported texture anisotropy')
 if type(filter_mode) is not int or filter_mode not in [1,2]:raise ValueError('Only measured linear min/mag modes admitted')
 if quality_anisotropy not in ['Disable','Enable','ForceEnable']:raise ValueError('Unknown native quality anisotropy')
 if source_backend not in ['Direct3D11','Direct3D12']:raise ValueError('Native backend policy unmeasured')
 effective=texture_anisotropy
 if quality_anisotropy=='Disable' or effective==0:effective=1
 elif quality_anisotropy=='ForceEnable':effective=max(9,effective)
 effective=max(1,effective)
 return dict(texture_declared_anisotropy=texture_anisotropy,source_declared_filter_mode=filter_mode,source_quality_anisotropy=quality_anisotropy,source_backend=source_backend,effective_anisotropy=effective,effective_min_filter='Linear',effective_mag_filter='Linear',effective_mip_filter='Linear' if filter_mode==2 or effective>1 else 'Nearest',policy_source='https://docs.unity3d.com/kr/6000.0/ScriptReference/Texture-anisoLevel.html',fitted_correction=False)
