"""Correct the reference macro to the actual HDRP SampleBias path, fixed bias zero."""
from pathlib import Path
import re
recipe=Path(__file__).with_name('run_house_implicit_surface_reference_v621.py')
code=recipe.read_text('utf-8-sig').replace("O=W/'house-implicit-surface-reference-v621'","O=W/'house-hdrp-bias-surface-reference-v623'")
code=code.replace('HouseImplicitSurfaceV621','HouseImplicitSurfaceV623')
code=code.replace("ap=W/'house-native-texture-mips-v619/HELPER-OWNERSHIP.v1.json'","ap=W/'house-implicit-surface-reference-v621/HELPER-OWNERSHIP.v1.json'").replace("len(audit['helpers'])==23","len(audit['helpers'])==24")
old="base=S/'candidates/HouseImplicitSurfaceV623.cs';helper="
new="base=O/'HouseImplicitSurfaceV623.cs';base.write_text((S/'candidates/HouseImplicitSurfaceV621.cs').read_text('utf-8-sig').replace('HouseImplicitSurfaceV621','HouseImplicitSurfaceV623'),'utf-8');helper="
assert code.count(old)==1;code=code.replace(old,new)
needle="    assert body.count('CBUFFER_START(UnityPerMaterial)')==1;"
addition="""    sdk=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/PackageManager/BuiltInPackages')
    variables=sdk/'com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl'
    config=sdk/'com.unity.render-pipelines.high-definition-config/Runtime/ShaderConfig.cs.hlsl'
    forward=W/'house-generated-contract-audit-v610/generated-forward.shader'
    assert '#define SUPPORT_GLOBAL_MIP_BIAS 1' in forward.read_text('utf-8-sig')
    assert '#define SHADEROPTIONS_GLOBAL_MIP_BIAS (1)' in config.read_text('utf-8-sig')
    import re
    macros=re.findall(r'#define SAMPLE_TEXTURE2D\\(textureName, samplerName, coord2\\) \\\\\\n\\s+PLATFORM_SAMPLE_TEXTURE2D_BIAS\\(textureName, samplerName, coord2,  _GlobalMipBias\\)',variables.read_text('utf-8-sig'))
    assert len(macros)==1,'Exact original HDRP macro required'
    marker='static float4x4 _OpenRPGWorldToObject;';assert body.count(marker)==1
    body=body.replace(marker,'// Exact HDRP 2D sampling macro with explicit zero-bias reference fixture.\\nstatic const float _GlobalMipBias=0.0;\\n#undef SAMPLE_TEXTURE2D\\n'+macros[0]+'\\n'+marker)
    for p in [recipe,S/'candidates/HouseImplicitSurfaceV621.cs',variables,config,forward]:pins[str(p)]=sha(p)
"""+needle
assert code.count(needle)==1;code=code.replace(needle,addition)
code=code.replace('Original SDK SAMPLE_TEXTURE2D in fragment stage; no explicit LOD replacement','Actual HDRP SampleBias macro in fragment stage; fixed global mip bias zero; no explicit LOD replacement')
code=code.replace('Generated surface shader on controlled plane; no prefab mesh, lighting/shadows or depth-host qualification','Generated surface shader with exact HDRP macro and explicit zero-bias fixture on controlled plane; no prefab mesh, lighting/shadows, arbitrary scene bias or depth-host qualification')
exec(compile(code,str(recipe)+'#faithful-hdrp-zero-bias-reference','exec'),globals())
