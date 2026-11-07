"""Compare native DX11/DX12 and WGSL DX12 with the original absolute tolerance."""
from pathlib import Path
recipe=Path(__file__).with_name('compare_house_float_transport_v633.py')
code=recipe.read_text('utf-8-sig')
for old,new in {
 'house-float-transport-reference-v632':'house-dx12-surface-reference-v636',
 'house-float-transport-comparison-v633':'house-dx12-surface-comparison-v637',
 'native_bc_vs_native_float':'native_dx11_vs_native_dx12',
 'native_float_vs_wgsl_float':'native_dx12_vs_wgsl_dx12',
 'transport_difference_confirmed_in_fixture':'api_difference_confirmed_in_fixture',
 'house_float_transport_comparison_complete':'house_dx12_source_surface_comparison_complete',
 'Retain original BC mip blocks and compare WGSL with native BC resources':'Record DX11/DX12 source profiles and retain both outcomes',
 'for p in [Path(__file__),N/':'for p in [Path(__file__),recipe,N/',
}.items():assert old in code,old;code=code.replace(old,new)
needle="run=read(N/'RUN-RESULT.v1.json');"
assert code.count(needle)==1;code=code.replace(needle,"assert read(N/'NATIVE-RESULT.v1.json')['graphics_api']=='Direct3D12'\n"+needle)
exec(compile(code,str(recipe)+'#dx12-source-comparison','exec'),globals())
