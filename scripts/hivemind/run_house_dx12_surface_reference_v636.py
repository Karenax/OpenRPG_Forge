"""Run the unchanged source shader on DX12 to isolate the API/compiler boundary."""
from pathlib import Path
parent=Path(__file__).with_name('run_house_hdrp_bias_surface_reference_v623.py')
exec(compile(parent.read_text('utf-8-sig').split('exec(compile(code,')[0],str(parent)+'#prepare-recipe','exec'),globals())
code=code.replace('house-hdrp-bias-surface-reference-v623','house-dx12-surface-reference-v636').replace('HouseImplicitSurfaceV623','HouseDx12SurfaceV636')
code=code.replace("ap=W/'house-implicit-surface-reference-v621/HELPER-OWNERSHIP.v1.json'","ap=W/'house-float-transport-reference-v632/HELPER-OWNERSHIP.v1.json'").replace("len(audit['helpers'])==24","len(audit['helpers'])==28")
needle="    old=\"assert len(native['cases']) == 5"
assert code.count(needle)==1;code=code.replace(needle,"    code=code.replace('force-d3d11','force-d3d12')\n"+needle)
code=code.replace("native['graphics_api']=='Direct3D11'","native['graphics_api']=='Direct3D12'")
code=code.replace('for p in [recipe,S/','for p in [parent,recipe,S/')
code=code.replace('Generated surface shader with exact HDRP macro and explicit zero-bias fixture on controlled plane; no prefab mesh, lighting/shadows, arbitrary scene bias or depth-host qualification','Unchanged generated surface, native compressed resources, exact HDRP macro and zero bias on Direct3D12; API comparison diagnostic, no mesh/depth/Studio qualification')
exec(compile(code,str(parent)+'#dx12-source-api-control','exec'),globals())
