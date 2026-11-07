"""Compare the DXC diagnostic without claiming parity with the original compiler."""
from pathlib import Path
parent=Path(__file__).with_name('compare_house_dx12_surface_v637.py')
text=parent.read_text('utf-8-sig').replace('house-dx12-surface-reference-v636','house-dxc-surface-reference-v638').replace('house-dx12-surface-comparison-v637','house-dxc-surface-comparison-v639').replace('native_dx11_vs_native_dx12','native_original_vs_native_dxc').replace('native_dx12_vs_wgsl_dx12','native_dxc_vs_wgsl_dx12').replace('api_difference_confirmed_in_fixture','compiler_difference_confirmed_in_fixture').replace('house_dx12_source_surface_comparison_complete','house_dxc_diagnostic_surface_comparison_complete').replace('Record DX11/DX12 source profiles and retain both outcomes','Retain original and DXC diagnostic outcomes; no original compiler qualification')
exec(compile(text,str(parent)+'#dxc-compiler-control','exec'),globals())
